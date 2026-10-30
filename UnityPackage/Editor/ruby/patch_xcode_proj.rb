#!/usr/bin/env ruby
# frozen_string_literal: true

PACKAGE_VERSION = '1.15.1'
PACKAGE_URL     = 'https://github.com/Octopus-Community/octopus-sdk-swift.git'
PRODUCTS        = ['Octopus', 'OctopusUI']

REQUIRED_BUNDLES = [
  'OctopusSdkSwift_OctopusCore.bundle',
  'OctopusSdkSwift_OctopusUI.bundle',
  'SwiftProtobuf_SwiftProtobuf.bundle',
  'swift-nio-ssl_NIOSSL.bundle',
  'swift-nio_NIOPosix.bundle'
]

PROJECT_NAME     = 'Unity-iPhone'
FRAMEWORK_TARGET = 'UnityFramework'

# ------------------------------------------------------------
# Locate vendored xcodeproj
# ------------------------------------------------------------

SCRIPT_DIR = File.expand_path(File.dirname(__FILE__))
VENDOR_BUNDLE_DIR = File.join(SCRIPT_DIR, 'vendor', 'bundle')

abort '[Octopus SDK] vendor/bundle not found' unless Dir.exist?(VENDOR_BUNDLE_DIR)

# EVERY vendored gem's `lib`, not just xcodeproj's. `xcodeproj.rb` requires `claide` on its
# third line and reaches nanaimo, colored2, CFPropertyList, atomos and rexml right after, so
# putting only xcodeproj on the load path worked by accident — on a machine where a CocoaPods
# install had already dropped those gems into the interpreter's own gem path — and failed with
# `cannot load such file -- claide` everywhere else, leaving an Xcode export that links no
# native SDK. Every gem here is pure Ruby, no native extension, so the tree is usable from any
# interpreter despite the `3.2.0` in its path.
gem_libs = Dir.glob(File.join(VENDOR_BUNDLE_DIR, 'ruby', '*', 'gems', '*', 'lib')).sort

abort '[Octopus SDK] xcodeproj gem not found' if gem_libs.grep(/xcodeproj-/).empty?

$LOAD_PATH.unshift(*gem_libs)
require 'xcodeproj'

# ------------------------------------------------------------
# Arguments
# ------------------------------------------------------------

abort 'Usage: ruby patch_xcode_proj.rb <path-to-xcodeproj>' unless ARGV[0]
PROJECT_PATH = ARGV[0]
abort 'Xcode project not found' unless File.exist?(PROJECT_PATH)

# ------------------------------------------------------------
# Open project
# ------------------------------------------------------------

project = Xcodeproj::Project.open(PROJECT_PATH)
pbxproj = project.root_object
abort 'Project name mismatch' unless pbxproj.name == PROJECT_NAME

# ------------------------------------------------------------
# 1. Resolve UnityFramework target
# ------------------------------------------------------------

framework_target =
  project.targets.find { |t| t.name == FRAMEWORK_TARGET } ||
  abort("[Octopus SDK] Target not found: #{FRAMEWORK_TARGET}")

# ------------------------------------------------------------
# 2. Add or update the Swift Package reference
# ------------------------------------------------------------

# Internal tooling (pm-tools native-dev): OCTOPUS_IOS_DEV_PACKAGE_URL and
# OCTOPUS_IOS_DEV_REVISION, exported together by `native-dev prepare` during a native
# dev-source window, point the package at an unreleased native commit. Inert when unset:
# the public URL at PACKAGE_VERSION, as always.
dev_url      = ENV.fetch('OCTOPUS_IOS_DEV_PACKAGE_URL', '')
dev_revision = ENV.fetch('OCTOPUS_IOS_DEV_REVISION', '')
if dev_url.empty? != dev_revision.empty?
  abort '[Octopus SDK] OCTOPUS_IOS_DEV_PACKAGE_URL and OCTOPUS_IOS_DEV_REVISION must be set together'
end
package_url = dev_url.empty? ? PACKAGE_URL : dev_url
requirement =
  if dev_url.empty?
    { 'kind' => 'exactVersion', 'version' => PACKAGE_VERSION }
  else
    { 'kind' => 'revision', 'revision' => dev_revision }
  end

# An Append-mode export keeps the reference of the previous build, which may carry the other
# URL: the public one before a window, the dev one during it, the public one again after it.
# Ours is a remote reference on either URL, or one our products link that is pinned to a
# revision of an Octopus-Community `octopus-sdk-swift*` repository — a dev reference always
# is, and its exact URL is unknown once the variables are unset. Anything else (a local
# package, the host's own fork, another remote) is the host's and left alone.
OCTOPUS_REPO_URL = %r{\Ahttps://github\.com/Octopus-Community/octopus-sdk-swift[A-Za-z0-9-]*\.git\z}.freeze
octopus_deps = framework_target.package_product_dependencies.select do |d|
  PRODUCTS.include?(d.product_name)
end
candidates =
  pbxproj.package_references.select do |pkg|
    next false unless pkg.is_a?(Xcodeproj::Project::Object::XCRemoteSwiftPackageReference)
    next true if [PACKAGE_URL, package_url].include?(pkg.repositoryURL)

    OCTOPUS_REPO_URL.match?(pkg.repositoryURL.to_s) &&
      pkg.requirement.is_a?(Hash) && pkg.requirement['kind'] == 'revision' &&
      octopus_deps.any? { |d| d.package.equal?(pkg) }
  end
package = candidates.find { |pkg| pkg.repositoryURL == package_url } || candidates.first

unless package
  package = project.new(Xcodeproj::Project::Object::XCRemoteSwiftPackageReference)
  pbxproj.package_references << package
end
# Xcodeproj compares objects by content: a duplicate identical to the survivor would be
# removed together with it, and re-pointing a link onto an "equal" object is a no-op. So the
# duplicates are made distinct first; then the products that linked one move to the survivor,
# in every target (removing a duplicate would leave their link empty); then they go. A product
# with no package at all — a local package added as a folder — is the host's: left alone.
stale_refs = candidates.reject { |c| c.equal?(package) }
stale_refs.each { |stale| stale.repositoryURL = "removed:#{stale.uuid}" }
project.targets.each do |t|
  next unless t.respond_to?(:package_product_dependencies)

  t.package_product_dependencies.each do |d|
    d.package = package if stale_refs.any? { |c| c.equal?(d.package) }
  end
end
stale_refs.each(&:remove_from_project)
package.repositoryURL = package_url
# Always (re)write the requirement: an Xcode project exported in Append mode keeps the
# reference from the previous SDK version, and leaving it untouched would pin the old one.
package.requirement = requirement
puts "[Octopus SDK] native-dev: Octopus package at revision #{dev_revision}" unless dev_url.empty?

# ------------------------------------------------------------
# 3. Link SwiftPM products
# ------------------------------------------------------------

PRODUCTS.each do |product_name|
  next if framework_target.package_product_dependencies.any? do |d|
    d.product_name == product_name
  end

  dep = project.new(Xcodeproj::Project::Object::XCSwiftPackageProductDependency)
  dep.product_name = product_name
  dep.package = package

  framework_target.package_product_dependencies << dep
end

# ------------------------------------------------------------
# 4. Add SwiftPM resource bundles to Copy Bundle Resources
# ------------------------------------------------------------

resources_phase = framework_target.resources_build_phase

products_group =
  project.main_group['Products'] ||
  project.main_group.new_group('Products')

REQUIRED_BUNDLES.each do |bundle_name|
  next if resources_phase.files.any? { |f| f.file_ref&.path == bundle_name }

  file_ref = products_group.new_file(bundle_name)
  file_ref.source_tree = 'BUILT_PRODUCTS_DIR'
  file_ref.include_in_index = '0'

  resources_phase.add_file_reference(file_ref)

  puts "[Octopus SDK] Added #{bundle_name} to Copy Bundle Resources"
end

# ------------------------------------------------------------
# 5. Save project
# ------------------------------------------------------------

project.save
puts '[Octopus SDK] Xcode project patched successfully'
