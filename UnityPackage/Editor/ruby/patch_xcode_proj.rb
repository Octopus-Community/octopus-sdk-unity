#!/usr/bin/env ruby
# frozen_string_literal: true

PACKAGE_VERSION = '1.14.0'
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
# 1. Add Swift Package reference
# ------------------------------------------------------------

package =
  pbxproj.package_references.find do |pkg|
    pkg.is_a?(Xcodeproj::Project::Object::XCRemoteSwiftPackageReference) &&
      pkg.repositoryURL == PACKAGE_URL
  end

unless package
  package = project.new(Xcodeproj::Project::Object::XCRemoteSwiftPackageReference)
  package.repositoryURL = PACKAGE_URL
  package.requirement = {
    'kind' => 'exactVersion',
    'version' => PACKAGE_VERSION
  }
  pbxproj.package_references << package
end

# ------------------------------------------------------------
# 2. Resolve UnityFramework target
# ------------------------------------------------------------

framework_target =
  project.targets.find { |t| t.name == FRAMEWORK_TARGET } ||
  abort("[Octopus SDK] Target not found: #{FRAMEWORK_TARGET}")

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
