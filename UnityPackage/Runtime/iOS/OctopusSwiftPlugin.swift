import Foundation
@_spi(OctopusInternalTesting) import Octopus
import OctopusCore
import OctopusUI
import SwiftUI
import UIKit
import Combine

private let COLOR_SCHEME_TYPE_LIGHT: Int32 = 1
private let COLOR_SCHEME_TYPE_DARK: Int32 = 2
private let PROFILE_FIELD_NICKNAME: Int32 = 0
private let PROFILE_FIELD_BIO: Int32 = 1
private let PROFILE_FIELD_PICTURE: Int32 = 2

private var octopus: OctopusSDK?
private var lifecycleUsesSSO = false
private weak var presentedViewController: UIViewController?
private var tokenCheckedContinuation: CheckedContinuation<String, Never>?
private var lightColorScheme: OctopusTheme.Colors?
private var darkColorScheme: OctopusTheme.Colors?

private var colorSchemeType: Int32 = 0  // 0 = System, 1 = Light, 2 = Dark
private var navBarUsesPrimaryColor: Bool = false
private var logo: UIImage?
private var appName: String?
private var fonts: OctopusTheme.Fonts?

// Orientation forced on the Octopus UI. 0 = None (follow the game/device), 1 = Portrait, 2 = Landscape.
// Mirrors OctopusThemeSettings.ForcedOrientationType. Read by OctopusHostingController on present.
private var forcedOrientation: Int32 = 0
// Records that we actually armed the forced orientation for the CURRENT presentation. The dismiss-time
// cleanup keys off this, NOT off the mutable forcedOrientation global — otherwise a mid-session change
// to forcedOrientation would skip the cleanup and leave the app mask stuck widened (game never restored).
private var didForceOrientation = false
// The window's interface orientation captured just before forcing, so we can restore the game's own
// orientation on dismiss — correct whether the game is landscape or portrait.
private var preForcedInterfaceOrientation: UIInterfaceOrientation = .unknown

private var octopusController: OctopusHostingController?
// Tracks whether the retained controller currently hosts the main feed. Used to decide
// whether an Open()/OpenPost("") (main-feed) request can reuse it or must rebuild.
private var octopusControllerShowsMainFeed = false
var notSeenNotifCancellable: AnyCancellable?
var groupsCancellable: AnyCancellable?
private var profileCancellable: AnyCancellable?
var eventsCancellable: AnyCancellable?
var communityAccessCancellable: AnyCancellable?

// Toggled from C# (OctopusSdkSetUrlInterceptionEnabled). When false the SDK opens URLs
// itself (in-app browser); when true taps are forwarded to Unity for the host to decide.
private var urlInterceptionEnabled = false
private var profileInterceptionEnabled = false
private var octopusControllerNavigationMode: Int32 = -1

@_cdecl("OctopusSdkSetProfileInterceptionEnabled")
public func OctopusSdkSetProfileInterceptionEnabled(enabled: Int32) {
    DispatchQueue.main.async {
        profileInterceptionEnabled = enabled != 0
        configureProfileInterception()
    }
}

private func configureProfileInterception() {
    guard profileInterceptionEnabled else {
        octopus?.set(onNavigateToProfileCallback: nil)
        return
    }
    octopus?.set(onNavigateToProfileCallback: { clientUserId in
        // Dismiss completely so Unity's player loop is resumed before delivering the tap.
        Task { @MainActor in
            await discardLifecycleUI()
            let data = try? JSONSerialization.data(withJSONObject: ["clientUserId": clientUserId])
            if let data, let json = String(data: data, encoding: .utf8) {
                sendUnityMessage("OctopusChannel", "OnNavigateToProfile", json)
            }
        }
    })
}

private func decodeNavigationMode(_ code: Int32, profile: Bool = false) -> OctopusNavigationMode {
    // -1 preserves each screen's own default. Unity `Automatic` names the automatic container, `NavigationStack` the navigationStack one.
    if code == 0 || (code == -1 && profile) { return .navigationStack }
    return .automatic
}

// MARK: - Loop-independent lane (reverse P/Invoke)
// The Swift→C# direction. C# registers these once at Initialize; we call them directly (off the
// Unity player loop) for events and the token request, so they work while the loop is paused.
// Signatures must match OctopusSDK.IosBridgeCallbacks.cs (see plan Global Constraints ABI).
// public (not private): these are parameter types of the public @_cdecl OctopusSdkSetBridgeCallbacks,
// and Swift requires a public function's parameter types to be at least as accessible as the function.
public typealias EventCallback = @convention(c) (UnsafePointer<CChar>?) -> Void
public typealias TokenRequestCallback = @convention(c) () -> Void
public typealias BridgeShareSignCallback = @convention(c) (UnsafePointer<CChar>?) -> Void
// Synchronous (returns a value): resolves the host's URL-interception strategy off the player loop.
// 0 = HandledByApp, 1 = HandledByOctopus. Mirrors Android's loop-independent resolveUrlStrategy.
public typealias UrlStrategyCallback = @convention(c) (UnsafePointer<CChar>?) -> Int32
private var eventCallback: EventCallback?
private var tokenRequestCallback: TokenRequestCallback?
private var bridgeShareSignCallback: BridgeShareSignCallback?
private var urlStrategyCallback: UrlStrategyCallback?
private var bridgeShareSignContinuation: CheckedContinuation<String, Error>?
private enum BridgeShareSignError: Error { case noCallback, superseded, hostFailed }

@_cdecl("OctopusSdkSetBridgeCallbacks")
public func OctopusSdkSetBridgeCallbacks(eventCb: EventCallback?, tokenCb: TokenRequestCallback?, signCb: BridgeShareSignCallback?, urlStrategyCb: UrlStrategyCallback?) {
    eventCallback = eventCb
    tokenRequestCallback = tokenCb
    bridgeShareSignCallback = signCb
    urlStrategyCallback = urlStrategyCb
}

// MARK: - Bridge Root View

private struct OctopusBridgeRootView: View {
    let octopus: OctopusSDK
    let navBarTitle: OctopusMainFeedTitle?
    let coloredNavBar: Bool
    let initialScreen: OctopusInitialScreen
    let navigationMode: OctopusNavigationMode
    @Binding var notificationUserInfo: [AnyHashable: Any]?
    let theme: OctopusTheme

    var body: some View {
        OctopusHomeScreen(
            octopus: octopus,
            mainFeedNavBarTitle: navBarTitle,
            mainFeedColoredNavBar: coloredNavBar,
            initialScreen: initialScreen,
            navigationMode: navigationMode,
            notificationUserInfo: $notificationUserInfo
        )
        .environment(\.octopusTheme, theme)
    }
}

// Holds the userInfo for the next presentation. The SwiftUI binding is reset to nil
// by the SDK once the notification has been consumed.
private var pendingNotificationUserInfo: [AnyHashable: Any]?

@_cdecl("OctopusSdkInitialize")
public func OctopusSdkInitialize(
    apiKey: UnsafePointer<Int8>, connectionMode: UnsafePointer<Int8>,
    appManagedFields: UnsafePointer<Int32>, appManagedFieldsCount: Int32,
    apiServerHost: UnsafePointer<Int8>, apiServerPort: Int32
) {
    let key = String(cString: apiKey)
    let connMode = parseConnectionMode(connectionMode, appManagedFields, appManagedFieldsCount)
    let hostStr = String(cString: apiServerHost)
    do {
        // Custom server host (e.g. a staging endpoint) when provided; otherwise the SDK default (prod).
        let configuration: OctopusSDK.Configuration =
            hostStr.isEmpty ? .init()
                            : .init(apiServer: try .init(host: hostStr, port: Int(apiServerPort)))
        communityDataCancellable = nil
        octopus = try OctopusSDK(apiKey: key, connectionMode: connMode, configuration: configuration)
        lifecycleUsesSSO = String(cString: connectionMode) == "sso"
        configureLifecycleBridge()
    } catch {
        print("Octopus Init Error: \(error)")
    }
}

private func configureLifecycleBridge() {
    bindCommunityDataObservation()
    configureProfileInterception()
    clearClientPostSession()
    octopus?.set(onNavigateToURLCallback: { url in
        // Resolve the strategy SYNCHRONOUSLY over the loop-independent lane (mirrors Android's
        // resolveUrlStrategy): the host's NavigateToUrlHandler runs in C# off the player loop, so
        // it works while Octopus is shown and the loop is paused — no UnitySendMessage round-trip,
        // no tearing down the UI to flush it. Codes match UrlOpeningStrategy.
        guard urlInterceptionEnabled, let resolve = urlStrategyCallback else {
            return .handledByOctopus
        }
        let urlString = url.absoluteString
        let strategy = urlString.withCString { resolve($0) }
        if strategy == 0 {
            // HandledByApp → the host takes over: leave the community (dismiss resumes the loop).
            OctopusSdkClose(keepState: true)
        } else {
            // HandledByOctopus → open the device's system browser; the community STAYS open
            // (matches Android's openUrlInOctopus). Return .handledByApp below so the SDK does not
            // also open its own in-app browser.
            urlString.withCString { OctopusSdkOpenUrlInOctopus(url: $0) }
        }
        return .handledByApp
    })
    octopus?.set(displayClientObjectCallback: { objectId in
        // Always a "leave Octopus" action → dismiss (resumes the loop), then notify.
        OctopusSdkClose(keepState: true)
        sendUnityMessage("OctopusChannel", "OnNavigateToClientObject", objectId)
    })
    octopus?.set(groupAccessDeniedCallback: { groupId in
        sendUnityMessage("OctopusChannel", "OnGroupAccessDenied", groupId)
    })
    // Unity's Bundle.main doesn't include .lproj folders for all languages,
    // so the SDK's default language detection (Bundle.main.preferredLocalizations)
    // returns "en" regardless of the device language.
    // Override with the actual device locale so gRPC Accept-Language header is correct.
    octopus?.overrideDefaultLocale(with: Locale.current)
    notSeenNotifCancellable = octopus?.$notSeenNotificationsCount.sink { count in
        sendUnityMessage("OctopusChannel", "OnNotSeenNotificationsCount", String(count) )
    }
    profileCancellable?.cancel()
    profileCancellable = octopus?.$profile.sink { profile in
        sendUnityMessage("OctopusChannel", "OnProfileChanged", profileToJson(profile))
    }
    groupsCancellable = octopus?.$groups.sink { groups in
        sendUnityMessage("OctopusChannel", "OnGroupsChanged", groupsToJson(groups))
    }
    communityAccessCancellable = octopus?.$hasAccessToCommunity.sink { hasAccess in
        sendUnityMessage("OctopusChannel", "OnHasAccessToCommunity", hasAccess ? "true" : "false")
    }
    eventsCancellable = octopus?.eventPublisher.sink { event in
        let json = eventToJson(event)
        // Loop-independent lane: delivers even while the Octopus UI is up and the loop is paused.
        if let cb = eventCallback {
            json.withCString { cb($0) }
        } else {
            sendUnityMessage("OctopusChannel", "OnOctopusEventJson", json)
        }
    }
}

private func parseConnectionMode(
    _ connectionMode: UnsafePointer<Int8>, _ fieldsPtr: UnsafePointer<Int32>, _ fieldsCount: Int32
) -> Octopus.ConnectionMode {
    let deepLink: String? = nil
    let mode = String(cString: connectionMode)
    let fieldsBuf = UnsafeBufferPointer(
        start: fieldsPtr,
        count: Int(fieldsCount)
    )
    let fields: Set<Octopus.ConnectionMode.SSOConfiguration.ProfileField> =
        Set(
            fieldsBuf.compactMap { fieldValue in
                switch fieldValue {
                case PROFILE_FIELD_NICKNAME:
                    return .nickname
                case PROFILE_FIELD_BIO:
                    return .bio
                case PROFILE_FIELD_PICTURE:
                    return .picture
                default:
                    return nil
                }
            })
    if mode == "sso" {
        return .sso(
            .init(
                appManagedFields: fields,
                loginRequired: {
                    OctopusSdkClose(keepState: true)
                    sendUnityMessage("OctopusChannel", "OnLoginRequired", "")
                },
                modifyUser: { fieldToEdit in
                    let field = fieldToString(fieldToEdit) ?? ""
                    OctopusSdkClose(keepState: true)
                    sendUnityMessage("OctopusChannel", "OnModifyUser", field)
                },
            )
        )
    }
    return .octopus(deepLink: deepLink)
}

private func fieldToString(_ profileField: Octopus.ConnectionMode.SSOConfiguration.ProfileField?) -> String?
{
    switch profileField {
    case .nickname:
        return "NICKNAME"
    case .bio:
        return "BIO"
    case .picture:
        return "PICTURE"
    default:
        return nil
    }
}

@_cdecl("OctopusSdkOpen")
public func OctopusSdkOpen(payloadJson: UnsafePointer<Int8>, navigationMode: Int32) {
    let json = String(cString: payloadJson)
    presentHome(initialScreen: .mainFeed, payloadJson: json.isEmpty ? nil : json, navigationMode: navigationMode)
}

@_cdecl("OctopusSdkOpenGroup")
public func OctopusSdkOpenGroup(groupId: UnsafePointer<Int8>, navigationMode: Int32) {
    let gid = String(cString: groupId)
    presentHome(initialScreen: gid.isEmpty ? .mainFeed : .group(.init(groupId: gid)), payloadJson: nil, navigationMode: navigationMode)
}

@_cdecl("OctopusSdkOpenPost")
public func OctopusSdkOpenPost(postId: UnsafePointer<Int8>, navigationMode: Int32) {
    let pid = String(cString: postId)
    presentHome(initialScreen: pid.isEmpty ? .mainFeed : .post(.init(postId: pid)), payloadJson: nil, navigationMode: navigationMode)
}

// Builds the bridge-share signing closure handed to OctopusPrefilledPost. Defined out-of-line so
// its throwing async body isn't inside the Task { } closure (which otherwise makes Task-init
// overload resolution ambiguous). The host signs on its backend; if it can't (no callback, or the
// host signer throws / returns empty) we THROW so the SDK aborts the post with a clean error rather
// than publishing an empty signature, which the server rejects and leaves the editor spinning.
private func makeBridgeShareSignClosure() -> @Sendable (_ bridgeFingerprint: String) async throws -> String {
    return { fingerprint in
        guard let cb = bridgeShareSignCallback else { throw BridgeShareSignError.noCallback }
        return try await withCheckedThrowingContinuation { continuation in
            // Register the continuation BEFORE invoking C#. The C# callback can resolve synchronously
            // (a host signer that throws or returns immediately runs entirely inside cb($0)), firing
            // the reply right here — if we stored the continuation after cb(), that reply would
            // resume a nil continuation (no-op) and this await would hang forever.
            bridgeShareSignContinuation?.resume(throwing: BridgeShareSignError.superseded) // overlap guard
            bridgeShareSignContinuation = continuation
            fingerprint.withCString { cb($0) }
        }
    }
}

@_cdecl("OctopusSdkOpenCreatePost")
public func OctopusSdkOpenCreatePost(
    text: UnsafePointer<Int8>, topicId: UnsafePointer<Int8>, imagePath: UnsafePointer<Int8>,
    ctaLabel: UnsafePointer<Int8>, ctaUrl: UnsafePointer<Int8>, hasSigner: Int32, navigationMode: Int32
) {
    let textStr = String(cString: text)
    let topicStr = String(cString: topicId)
    let pathStr = String(cString: imagePath)
    let ctaLabelStr = String(cString: ctaLabel)
    let ctaUrlStr = String(cString: ctaUrl)
    let wantsSigner = hasSigner != 0
    Task {
        let imageData = pathStr.isEmpty ? nil : await fetchImageData(fromPathOrUrl: pathStr)

        // CTA marshalling guarantees both fields are set together (or both empty).
        var cta: OctopusPrefilledPost.CTA? = nil
        if !ctaLabelStr.isEmpty, !ctaUrlStr.isEmpty, let url = URL(string: ctaUrlStr) {
            do {
                cta = try OctopusPrefilledPost.CTA(url: url, label: ctaLabelStr)
            } catch {
                print("OctopusSdkOpenCreatePost: invalid CTA (\(error)); dropping CTA")
            }
        }

        // Hand the SDK-computed fingerprint to C# over the loop-independent lane; await the JWT.
        // The closure is built out-of-line (see makeBridgeShareSignClosure) to keep its throwing
        // async body out of this Task { } closure.
        let sign: (@Sendable (_ bridgeFingerprint: String) async throws -> String)? =
            wantsSigner ? makeBridgeShareSignClosure() : nil

        // Since iOS 1.13.0 text and image are both optional, so a topic-only (or CTA-only)
        // request builds a real payload instead of being downgraded to the empty editor —
        // same shape as the Kotlin bridge's openCreatePost. init still throws on content
        // that fails the editor's publish-time validation; that falls back to nil.
        //
        // A fully empty request (OpenCreatePost() with no prefill) must stay `nil`: iOS
        // records the post's creationSource from the presence of the prefilled object
        // (nil → user, non-nil → prefilledFromClient), whereas Android derives it from the
        // content — so a non-nil empty prefill would make a blank editor report
        // prefilledFromClient on iOS only.
        let info: OctopusInitialScreen.CreatePostScreenInfo
        if textStr.isEmpty, imageData == nil, topicStr.isEmpty, cta == nil {
            info = .init(prefilledPost: nil)
        } else {
            do {
                let prefilled = try OctopusPrefilledPost(
                    text: textStr.isEmpty ? nil : textStr,
                    image: imageData,
                    topicId: topicStr.isEmpty ? nil : topicStr,
                    cta: cta,
                    sign: sign
                )
                info = .init(prefilledPost: prefilled)
            } catch {
                print("OctopusSdkOpenCreatePost: invalid prefilled post (\(error)); opening empty editor")
                info = .init(prefilledPost: nil)
            }
        }
        presentHome(initialScreen: .createPost(info), payloadJson: nil, navigationMode: navigationMode)
    }
}

@_cdecl("OctopusSdkSetUrlInterceptionEnabled")
public func OctopusSdkSetUrlInterceptionEnabled(enabled: Int32) {
    urlInterceptionEnabled = enabled != 0
}

@_cdecl("OctopusSdkOpenUrlInOctopus")
public func OctopusSdkOpenUrlInOctopus(url: UnsafePointer<Int8>) {
    let urlStr = String(cString: url)
    guard let urlObj = URL(string: urlStr) else { return }
    DispatchQueue.main.async {
        UIApplication.shared.open(urlObj)
    }
}

@_cdecl("OctopusSdkOpenProfile")
public func OctopusSdkOpenProfile(clientUserId: UnsafePointer<Int8>, navigationMode: Int32) {
    let id = String(cString: clientUserId).trimmingCharacters(in: .whitespacesAndNewlines)
    presentHome(initialScreen: .mainFeed, payloadJson: nil, navigationMode: navigationMode,
                opensProfile: true, profileClientUserId: id.isEmpty ? nil : id)
}

@_cdecl("OctopusSdkOpenActivity")
public func OctopusSdkOpenActivity(navigationMode: Int32) {
    DispatchQueue.main.async {
        let screen: OctopusInitialScreen
        // `core` is package-visible in the Swift SDK; same reflection as React Native and
        // the debug config endpoint below, confined to reading the current profile id.
        if let sdk = octopus,
           let core = Mirror(reflecting: sdk).children.first(where: { $0.label == "core" })?.value as? OctopusSDKCore,
           let id = core.profileRepository.profile?.id {
            screen = .activity(.init(profileId: id))
        } else {
            screen = .mainFeed
        }
        presentHome(initialScreen: screen, payloadJson: nil, navigationMode: navigationMode)
    }
}

private func presentHome(initialScreen: OctopusInitialScreen, payloadJson: String?,
                         navigationMode: Int32 = -1, opensProfile: Bool = false,
                         profileClientUserId: String? = nil) {
    // Decode the octopus payload (if any) into the userInfo["data"] shape the SDK expects.
    var userInfo: [AnyHashable: Any]? = nil
    if let payloadJson,
       let data = payloadJson.data(using: .utf8),
       let obj = try? JSONSerialization.jsonObject(with: data) as? [String: Any] {
        userInfo = ["data": obj]
    }

    DispatchQueue.main.async {
        // Reuse the existing controller only when re-opening the main feed AND it already
        // hosts the main feed (preserves the user's place on close/reopen). A specific
        // destination, a notification payload, or a main-feed request that follows a
        // non-feed screen (e.g. the create-post composer) rebuilds it — otherwise a stale
        // screen would be re-presented.
        let wantsMainFeed = !opensProfile && (userInfo == nil) && isMainFeed(initialScreen)
        if !wantsMainFeed || !octopusControllerShowsMainFeed || octopusControllerNavigationMode != navigationMode {
            octopusController?.dismiss(animated: false)
            octopusController = nil
        }
        guard let sdk = octopus else {
            print("OctopusSdkOpen: SDK not initialized. Call OctopusSdkInitialize first.")
            return
        }
        guard let presentingVC = topViewController() else { return }

        pendingNotificationUserInfo = userInfo

        if octopusController == nil {
            let navBarTitle: OctopusMainFeedTitle?
            if logo != nil {
                navBarTitle = OctopusMainFeedTitle(content: .logo, placement: .leading)
            } else if let name = appName, !name.isEmpty {
                navBarTitle = OctopusMainFeedTitle(content: .text(.init(text: name)), placement: .leading)
            } else {
                navBarTitle = nil
            }
            let themeFonts = fonts ?? OctopusTheme.Fonts()
            let effectiveColorScheme = resolveColorScheme()

            // When a notification is pending, it drives navigation — keep initialScreen .mainFeed.
            let effectiveScreen: OctopusInitialScreen = (userInfo != nil) ? .mainFeed : initialScreen

            let root = OctopusBridgeRootView(
                octopus: sdk,
                navBarTitle: navBarTitle,
                coloredNavBar: navBarUsesPrimaryColor,
                initialScreen: effectiveScreen,
                navigationMode: decodeNavigationMode(navigationMode),
                notificationUserInfo: Binding(
                    get: { pendingNotificationUserInfo },
                    set: { pendingNotificationUserInfo = $0 }
                ),
                theme: OctopusTheme(
                    colors: effectiveColorScheme,
                    fonts: themeFonts,
                    assets: .init(logo: logo)
                )
            )
            let view: AnyView
            if opensProfile {
                view = AnyView(OctopusProfileScreen(
                    octopus: sdk, clientUserId: profileClientUserId,
                    navigationMode: decodeNavigationMode(navigationMode, profile: true),
                    navBarLeadingAction: .close(onTap: { OctopusSdkClose() })
                ).environment(\.octopusTheme, OctopusTheme(
                    colors: effectiveColorScheme, fonts: themeFonts, assets: .init(logo: logo))))
            } else {
                view = AnyView(root)
            }
            octopusController = OctopusHostingController(rootView: view)
            octopusControllerNavigationMode = navigationMode
            octopusController?.modalPresentationStyle = .fullScreen
            octopusControllerShowsMainFeed = !opensProfile && isMainFeed(effectiveScreen)
        }

        if octopusController?.presentingViewController == nil {
            // Force orientation (if configured) BEFORE presenting, so UIKit's presentation-time
            // orientation query already sees the widened app mask (avoids the shouldAutorotate
            // assert). Capture the game's current orientation first, to restore it on dismiss.
            if let mask = forcedOrientationMask() {
                preForcedInterfaceOrientation =
                    presentingVC.view.window?.windowScene?.interfaceOrientation ?? .unknown
                didForceOrientation = true
                OctopusSetForcedOrientationMask(UInt(mask.rawValue))
            }
            presentingVC.present(octopusController!, animated: true)
        }
    }
}

private func isMainFeed(_ screen: OctopusInitialScreen) -> Bool {
    if case .mainFeed = screen { return true }
    return false
}

@_cdecl("OctopusSdkClose")
public func OctopusSdkClose(keepState: Bool = true) {
    DispatchQueue.main.async {
        octopusController?.dismiss(animated: true)
        if !keepState {
            octopusController = nil
            pendingNotificationUserInfo = nil
        }
    }
}

// MARK: - Community data (#167)

private var communityDataCancellable: AnyCancellable?
private var observingCommunityData = false
private var communityDataProfileId: String?
private var communityDataClientUserId: String?

private func communityDataToJson(_ data: OctopusCommunityData?) -> String {
    guard let data else { return "null" }
    var fields: [String: Any] = [
        "profileId": data.profileId,
        "messageCount": data.messageCount.map { $0 as Any } ?? NSNull(),
        "gamification": NSNull()
    ]
    if let standing = data.gamification {
        fields["gamification"] = [
            "level": standing.level,
            "score": standing.score.map { $0 as Any } ?? NSNull()
        ] as [String: Any]
    }
    guard let bytes = try? JSONSerialization.data(withJSONObject: fields),
          let json = String(data: bytes, encoding: .utf8) else { return "null" }
    return json
}

private func bindCommunityDataObservation() {
    communityDataCancellable = nil
    guard observingCommunityData, let sdk = octopus else { return }
    let publisher: AnyPublisher<OctopusCommunityData?, Never>
    if let profileId = communityDataProfileId {
        publisher = sdk.communityDataPublisher(profileId: profileId)
    } else if let clientUserId = communityDataClientUserId {
        publisher = sdk.communityDataPublisher(clientUserId: clientUserId)
    } else { return }
    communityDataCancellable = publisher.sink { data in
        sendUnityMessage("OctopusChannel", "OnCommunityDataChanged", communityDataToJson(data))
    }
}

private func stopCommunityDataObservation() {
    observingCommunityData = false
    communityDataProfileId = nil
    communityDataClientUserId = nil
    communityDataCancellable = nil
}

private func isValidCommunityMemberId(_ profileId: String?, _ clientUserId: String?) -> Bool {
    (profileId != nil) != (clientUserId != nil) && profileId != "" && clientUserId != ""
}

@_cdecl("OctopusSdkStartObservingCommunityData")
public func OctopusSdkStartObservingCommunityData(profileId: UnsafePointer<CChar>?, clientUserId: UnsafePointer<CChar>?) {
    // Copy borrowed nullable C strings before scheduling work.
    let profile = profileId.map { String(cString: $0) }
    let client = clientUserId.map { String(cString: $0) }
    guard isValidCommunityMemberId(profile, client) else { return }
    DispatchQueue.main.async {
        communityDataProfileId = profile
        communityDataClientUserId = client
        observingCommunityData = true
        bindCommunityDataObservation()
    }
}

@_cdecl("OctopusSdkStopObservingCommunityData")
public func OctopusSdkStopObservingCommunityData() {
    DispatchQueue.main.async { stopCommunityDataObservation() }
}

@_cdecl("OctopusSdkFetchCommunityData")
public func OctopusSdkFetchCommunityData(requestId: Int32, profileId: UnsafePointer<CChar>?, clientUserId: UnsafePointer<CChar>?) {
    let profile = profileId.map { String(cString: $0) }
    let client = clientUserId.map { String(cString: $0) }
    Task { @MainActor in
        guard let sdk = octopus else {
            sendUnityMessage("OctopusChannel", "OnFetchCommunityDataError", "\(requestId)\nSDK is not initialized.")
            return
        }
        guard isValidCommunityMemberId(profile, client) else {
            sendUnityMessage("OctopusChannel", "OnFetchCommunityDataError", "\(requestId)\nExactly one nonempty member id is required.")
            return
        }
        do {
            let data: OctopusCommunityData?
            if let profile {
                data = try await sdk.fetchCommunityData(profileId: profile)
            } else if let client {
                data = try await sdk.fetchCommunityData(clientUserId: client)
            } else { return }
            sendUnityMessage("OctopusChannel", "OnFetchCommunityDataResult", "\(requestId)\n\(communityDataToJson(data))")
        } catch {
            sendUnityMessage("OctopusChannel", "OnFetchCommunityDataError", "\(requestId)\nCould not fetch community data.")
        }
    }
}

// MARK: - Lifecycle (#161)

// Finish dismissal before releasing a controller: viewDidDisappear restores Unity's loop/orientation.
@MainActor
private func discardLifecycleUI() async {
    if let controller = octopusController, controller.presentingViewController != nil {
        await withCheckedContinuation { (continuation: CheckedContinuation<Void, Never>) in
            controller.dismiss(animated: false) { continuation.resume() }
        }
    }
    octopusController = nil
    octopusControllerShowsMainFeed = false
    pendingNotificationUserInfo = nil
}

@_cdecl("OctopusSdkSwitchCommunity")
public func OctopusSdkSwitchCommunity(
    requestId: Int32, apiKey: UnsafePointer<Int8>, connectionMode: UnsafePointer<Int8>,
    appManagedFields: UnsafePointer<Int32>, appManagedFieldsCount: Int32
) {
    // Copy borrowed C strings/arrays before crossing the asynchronous boundary.
    let key = String(cString: apiKey)
    let mode = parseConnectionMode(connectionMode, appManagedFields, appManagedFieldsCount)
    let usesSSO = String(cString: connectionMode) == "sso"
    Task { @MainActor in
        clearClientPostSession()
        await discardLifecycleUI()
        do {
            communityDataCancellable = nil
            if let current = octopus {
                try await current.switchCommunity(apiKey: key, connectionMode: mode)
            } else {
                octopus = try OctopusSDK(apiKey: key, connectionMode: mode, configuration: .init())
            }
            lifecycleUsesSSO = usesSSO
            configureLifecycleBridge()
            sendUnityMessage("OctopusChannel", "OnLifecycleResult", "\(requestId)\n")
        } catch {
            sendUnityMessage("OctopusChannel", "OnLifecycleError", "\(requestId)\nCommunity switch failed.")
        }
    }
}

@_cdecl("OctopusSdkReset")
public func OctopusSdkReset(requestId: Int32) {
    Task { @MainActor in
        clearClientPostSession()
        stopCommunityDataObservation()
        await discardLifecycleUI()
        do {
            // MagicLinkConnectionRepository.disconnectUser traps (not throws) in 1.13.2.
            // Do not reproduce the Flutter/RN wrapper crash for Octopus Auth.
            if octopus != nil && !lifecycleUsesSSO {
                sendUnityMessage("OctopusChannel", "OnLifecycleError", "\(requestId)\nReset requires SSO on iOS 1.13.2.")
                return
            }
            try await octopus?.disconnectUser()
            sendUnityMessage("OctopusChannel", "OnLifecycleResult", "\(requestId)\n")
        } catch {
            sendUnityMessage("OctopusChannel", "OnLifecycleError", "\(requestId)\nReset failed.")
        }
    }
}

@_cdecl("OctopusSdkStop")
public func OctopusSdkStop(requestId: Int32) {
    Task { @MainActor in
        clearClientPostSession()
        stopCommunityDataObservation()
        await discardLifecycleUI()
        // iOS has no public stop primitive. Best-effort disconnect, then release bridge ownership.
        do { if lifecycleUsesSSO { try await octopus?.disconnectUser() } }
        catch { print("[Octopus SDK] Stop: disconnection failed; releasing SDK instance.") }
        notSeenNotifCancellable = nil
        groupsCancellable = nil
        profileCancellable = nil
        eventsCancellable = nil
        communityAccessCancellable = nil
        octopus = nil
        lifecycleUsesSSO = false
        sendUnityMessage("OctopusChannel", "OnLifecycleResult", "\(requestId)\n")
    }
}

@_cdecl("OctopusSdkConnectUser")
public func OctopusSdkConnectUser(
    userId: UnsafePointer<Int8>, nickname: UnsafePointer<Int8>, bio: UnsafePointer<Int8>,
    picture: UnsafePointer<Int8>
) {
    let userIdStr = String(cString: userId)
    let nicknameStr = String(cString: nickname)
    let bioStr = String(cString: bio)
    let pictureStr = String(cString: picture)
    Task {
        let pictureData = await fetchImageData(fromPathOrUrl: pictureStr)
        do {
            try await octopus?.connectUser(
                ClientUser(
                    userId: userIdStr,
                    profile: ClientUser.Profile(
                        nickname: nicknameStr,
                        bio: bioStr,
                        picture: pictureData
                    )
                ),
                tokenProvider: {
                    // Loop-independent lane (matches Android requestToken): works during a mid-session
                    // refresh while Octopus is open and the loop is paused. Reply arrives via
                    // OctopusSdkSetUserToken, which resumes this continuation.
                    return await withCheckedContinuation { continuation in
                        // Register the continuation BEFORE notifying C#. The C# token provider can
                        // resolve synchronously (e.g. a cached token returned via Task.FromResult), so
                        // OctopusSdkSetUserToken may fire within cb()/the message dispatch; storing the
                        // continuation afterwards would resume nil and hang the connect.
                        //
                        // Overlap guard: the native SDK serializes token requests, but a mid-session
                        // refresh raises the chance of overlap. Resume any stale continuation (empty
                        // token = the superseded request fails gracefully) before storing the new one.
                        tokenCheckedContinuation?.resume(returning: "")
                        tokenCheckedContinuation = continuation
                        if let cb = tokenRequestCallback {
                            cb()
                        } else {
                            sendUnityMessage("OctopusChannel", "OnTokenRequested", "")
                        }
                    }
                }
            )
        } catch {
            print("Octopus connectUser failed: \(error)")
        }
        // Always signal completion so the Unity-side connect callback resolves.
        sendUnityMessage("OctopusChannel", "OnConnectUserCompleted", "")
    }
}

// Typed connection lane (#168). Legacy completion and its export above remain unchanged.
@_cdecl("OctopusSdkConnectUserWithResult")
public func OctopusSdkConnectUserWithResult(
    requestId: Int32, userId: UnsafePointer<Int8>, nickname: UnsafePointer<Int8>,
    bio: UnsafePointer<Int8>, picture: UnsafePointer<Int8>
) {
    let userIdStr = String(cString: userId)
    let nicknameStr = String(cString: nickname)
    let bioStr = String(cString: bio)
    let pictureStr = String(cString: picture)
    Task {
        guard let sdk = octopus else {
            sendConnectUserFailure(requestId, code: "other", message: "Call initialize() first")
            return
        }
        guard lifecycleUsesSSO else {
            sendConnectUserFailure(requestId, code: "other", message: "ConnectUser requires SSO mode")
            return
        }
        let pictureData = await fetchImageData(fromPathOrUrl: pictureStr)
        do {
            try await sdk.connectUser(
                ClientUser(userId: userIdStr, profile: ClientUser.Profile(
                    nickname: nicknameStr, bio: bioStr, picture: pictureData)),
                tokenProvider: {
                    await withCheckedContinuation { continuation in
                        tokenCheckedContinuation?.resume(returning: "")
                        tokenCheckedContinuation = continuation
                        if let cb = tokenRequestCallback { cb() }
                        else { sendUnityMessage("OctopusChannel", "OnTokenRequested", "") }
                    }
                }
            )
            sendUnityMessage("OctopusChannel", "OnConnectUserSucceeded", "\(requestId)\n")
        } catch let error as OctopusConnectUserError {
            let code: String
            let message: String
            switch error {
            case let .userBanned(reason):
                code = "userBanned"
                message = reason
            case let .profileError(errors):
                code = "profileError"
                message = errors.isEmpty ? "The supplied profile was rejected" : errors.map { detail in
                    (detail.field.map { "\($0): " } ?? "") + detail.message
                }.joined(separator: "\n")
            case .jwtError:
                code = "invalidToken"
                message = String(describing: error)
            case .communityAccessDenied:
                code = "communityAccessDenied"
                message = String(describing: error)
            default:
                code = "other"
                message = String(describing: error)
            }
            sendConnectUserFailure(requestId, code: code, message: message)
        } catch {
            sendConnectUserFailure(requestId, code: "other", message: String(describing: error))
        }
    }
}

private func sendConnectUserFailure(_ requestId: Int32, code: String, message: String) {
    let data = try? JSONSerialization.data(withJSONObject: ["code": code, "message": message])
    let json = data.flatMap { String(data: $0, encoding: .utf8) } ?? "{}"
    sendUnityMessage("OctopusChannel", "OnConnectUserFailed", "\(requestId)\n\(json)")
}

@_cdecl("OctopusSdkDisconnectUser")
public func OctopusSdkDisconnectUser() {
    octopus?.disconnectUser()
    Task {
        sendUnityMessage("OctopusChannel", "OnDisconnectUserCompleted", "")
    }
}

@_cdecl("OctopusSdkSetUserToken")
public func OctopusSdkSetUserToken(token: UnsafePointer<Int8>) {
    let tokenStr = String(cString: token)
    tokenCheckedContinuation?.resume(returning: tokenStr)
    tokenCheckedContinuation = nil
}

@_cdecl("OctopusSdkSetBridgeShareSignature")
public func OctopusSdkSetBridgeShareSignature(token: UnsafePointer<Int8>) {
    // Success path only: C# routes empty/null/failed results through OctopusSdkFailBridgeShareSignature
    // (which resumes throwing), so tokenStr is always a real JWT here.
    let tokenStr = String(cString: token)
    bridgeShareSignContinuation?.resume(returning: tokenStr)
    bridgeShareSignContinuation = nil
}

@_cdecl("OctopusSdkFailBridgeShareSignature")
public func OctopusSdkFailBridgeShareSignature() {
    // Host couldn't sign -> throw into the SDK so it aborts the post with a clean error
    // (rather than publishing an empty/invalid signature).
    bridgeShareSignContinuation?.resume(throwing: BridgeShareSignError.hostFailed)
    bridgeShareSignContinuation = nil
}

@_cdecl("OctopusSdkOverrideDefaultLocale")
public func OctopusSdkOverrideDefaultLocale(languageCode: UnsafePointer<Int8>) {
    let code = String(cString: languageCode)
    octopus?.overrideDefaultLocale(with: Locale(identifier: code))
}

@_cdecl("OctopusSdkSetAppName")
public func OctopusSdkSetAppName(name: UnsafePointer<Int8>) {
    let nameStr = String(cString: name)
    appName = nameStr
}

private func topViewController(
    base: UIViewController? = UIApplication.shared
        .connectedScenes
        .compactMap { $0 as? UIWindowScene }
        .flatMap { $0.windows }
        .first { $0.isKeyWindow }?
        .rootViewController
) -> UIViewController? {
    if let nav = base as? UINavigationController {
        return topViewController(base: nav.visibleViewController)
    }
    if let tab = base as? UITabBarController {
        return topViewController(base: tab.selectedViewController)
    }
    if let presented = base?.presentedViewController {
        return topViewController(base: presented)
    }
    return base
}

// Native pause/resume helper (OctopusUnityPause.mm). Stops/starts the Unity player loop and fires
// OnApplicationPause/OnApplicationFocus, matching Android backgrounding.
@_silgen_name("OctopusUnityPause")  private func OctopusUnityPause()
@_silgen_name("OctopusUnityResume") private func OctopusUnityResume()
// Widens Unity's app-level supported-orientation mask while the Octopus UI is shown (see the swizzle
// in OctopusUnityPause.mm). Pass a UIInterfaceOrientationMask.rawValue, or 0 to stop forcing.
@_silgen_name("OctopusSetForcedOrientationMask") private func OctopusSetForcedOrientationMask(_ mask: UInt)

// MARK: - Forced orientation helpers

// Maps the configured forcedOrientation (0/1/2) to a mask, or nil when not forcing.
// Orientation forcing relies on UIWindowScene.requestGeometryUpdate (iOS 16+). We deliberately do NOT
// use the private-API `UIDevice.setValue(_:forKey:"orientation")` rotation hack on older versions —
// it's an App Store review risk — so the feature is a no-op below iOS 16 (the community follows the
// game/device there, i.e. unchanged from before this feature). This is the single gate: returning nil
// makes every downstream path (VC overrides, arming, apply, restore) inert on iOS < 16.
private func forcedOrientationMask() -> UIInterfaceOrientationMask? {
    guard #available(iOS 16.0, *) else { return nil }
    switch forcedOrientation {
    case 1:  return .portrait
    case 2:  return .landscape
    default: return nil
    }
}

private func octopusForegroundWindowScene() -> UIWindowScene? {
    let scenes = UIApplication.shared.connectedScenes.compactMap { $0 as? UIWindowScene }
    return scenes.first { $0.activationState == .foregroundActive } ?? scenes.first
}

private func orientationMask(from o: UIInterfaceOrientation) -> UIInterfaceOrientationMask {
    switch o {
    case .portrait:           return .portrait
    case .portraitUpsideDown: return .portraitUpsideDown
    case .landscapeLeft:      return .landscapeLeft
    case .landscapeRight:     return .landscapeRight
    default:                  return .all
    }
}

// Drives pause/resume from the view-controller lifecycle so it covers EVERY dismissal path,
// including the SDK dismissing itself (OctopusHomeScreen calls presentationMode.dismiss()), which
// never routes through OctopusSdkClose. viewDidAppear/viewDidDisappear fire once per present/dismiss.
final class OctopusHostingController: UIHostingController<AnyView> {
    // Constrain the community to the forced orientation, if any. When not forcing, defer to the
    // default so behaviour is unchanged. This is the VC half of UIKit's orientation intersection;
    // the app-level half is widened by the swizzle in OctopusUnityPause.mm.
    override var supportedInterfaceOrientations: UIInterfaceOrientationMask {
        forcedOrientationMask() ?? super.supportedInterfaceOrientations
    }
    override var preferredInterfaceOrientationForPresentation: UIInterfaceOrientation {
        // forcedOrientationMask() is nil when not forcing OR below iOS 16, so this defers to the
        // default there and only pins an orientation when forcing is actually active.
        guard forcedOrientationMask() != nil else { return super.preferredInterfaceOrientationForPresentation }
        return forcedOrientation == 2 ? .landscapeRight : .portrait
    }

    override func viewDidAppear(_ animated: Bool) {
        super.viewDidAppear(animated)
        applyForcedOrientationIfNeeded()
        OctopusUnityPause()
    }
    override func viewDidDisappear(_ animated: Bool) {
        super.viewDidDisappear(animated)
        restoreOrientationIfNeeded()
        OctopusUnityResume()
    }

    // Actively rotate to the forced orientation even if the device is currently held the other way.
    // iOS 16+ only (forcedOrientationMask() is nil below 16); requestGeometryUpdate is public API.
    private func applyForcedOrientationIfNeeded() {
        guard #available(iOS 16.0, *), let mask = forcedOrientationMask() else { return }
        setNeedsUpdateOfSupportedInterfaceOrientations()
        view.window?.windowScene?.requestGeometryUpdate(.iOS(interfaceOrientations: mask))
    }

    // Stop forcing and restore the game's own orientation (captured at present time), so this works
    // whether the game is landscape or portrait. Keyed off didForceOrientation — the actual
    // present-time action — not the mutable forcedOrientation global, so a mid-session change to the
    // setting can't skip the cleanup and strand the app mask widened.
    private func restoreOrientationIfNeeded() {
        guard didForceOrientation else { return }
        didForceOrientation = false
        // Always undo the app-mask widening, even if we couldn't capture an orientation to rotate back to.
        OctopusSetForcedOrientationMask(0)
        defer { preForcedInterfaceOrientation = .unknown }
        // didForceOrientation is only ever set on iOS 16+ (forcing is gated there), so restoration is
        // reached only on 16+; requestGeometryUpdate is public API. No pre-16 private-API fallback.
        guard #available(iOS 16.0, *), preForcedInterfaceOrientation != .unknown else { return }
        octopusForegroundWindowScene()?.requestGeometryUpdate(
            .iOS(interfaceOrientations: orientationMask(from: preForcedInterfaceOrientation)))
    }
}

// Declare the external Unity C API function so Swift can call it.
@_silgen_name("UnitySendMessage")
private func UnitySendMessage(
    _ obj: UnsafePointer<CChar>, _ method: UnsafePointer<CChar>, _ msg: UnsafePointer<CChar>)

// Swift-friendly helper to send messages to Unity using Swift Strings.
@inline(__always)
private func sendUnityMessage(_ objectName: String, _ methodName: String, _ message: String) {
    objectName.withCString { objPtr in
        methodName.withCString { methodPtr in
            message.withCString { msgPtr in
                UnitySendMessage(objPtr, methodPtr, msgPtr)
            }
        }
    }
}

enum NetworkError: Error {
    case invalidURL
    case requestFailed
}

func fetchImageData(fromPathOrUrl url: String) async -> Data? {
    if url.hasPrefix("https://") || url.hasPrefix("http://") {
        return try? await fetchData(fromRemoteUrl: url)
    }
    return fetchData(fromLocalPath: url)
}

func fetchData(fromRemoteUrl urlString: String) async throws -> Data {
    guard let url = URL(string: urlString) else {
        throw NetworkError.invalidURL
    }
    let (data, response) = try await URLSession.shared.data(from: url)
    guard let httpResponse = response as? HTTPURLResponse,
        httpResponse.statusCode == 200
    else {
        throw NetworkError.requestFailed
    }
    return data
}

func fetchData(fromLocalPath path: String) -> Data? {
    let fileURL = URL(fileURLWithPath: path)
    do {
        let data = try Data(contentsOf: fileURL, options: .mappedIfSafe)
        return data
    } catch {
        print("Failed to read image data:", error)
        return nil
    }
}

@_cdecl("OctopusSdkSetLightColorScheme")
public func OctopusSdkSetLightColorScheme(
    primary: Int32, primaryLow: Int32, primaryHigh: Int32, onPrimary: Int32,
    link: Int32, background: Int32
) {
    OctopusSdkClose(keepState: false)
    lightColorScheme = colorsFrom(
        primary: primary, primaryLow: primaryLow, primaryHigh: primaryHigh,
        onPrimary: onPrimary, link: link, background: background
    )
}

@_cdecl("OctopusSdkSetDarkColorScheme")
public func OctopusSdkSetDarkColorScheme(
    primary: Int32, primaryLow: Int32, primaryHigh: Int32, onPrimary: Int32,
    link: Int32, background: Int32
) {
    OctopusSdkClose(keepState: false)
    darkColorScheme = colorsFrom(
        primary: primary, primaryLow: primaryLow, primaryHigh: primaryHigh,
        onPrimary: onPrimary, link: link, background: background
    )
}

/// Builds a theme color set from the six packed RGBA ints the C# side sends.
///
/// Every slot is optional, per channel: 0 (fully transparent) means the host left that color
/// disabled, and the pinned SDK default must stand. `OctopusTheme.Colors.init` takes
/// `primarySet`/`onPrimary`/`link`/`background` as optionals defaulting to nil (checked in
/// `Sources/OctopusUI/Theme/Theme.swift` at Octopus iOS 1.13.2), so nil is how a slot is left
/// alone. `ColorSet` itself has three non-optional members, so a partially set primary set is
/// filled from a default-constructed `Colors` — the SDK's own values, never a copy of them here.
private func colorsFrom(
    primary: Int32, primaryLow: Int32, primaryHigh: Int32, onPrimary: Int32,
    link: Int32, background: Int32
) -> OctopusTheme.Colors {
    let defaults = OctopusTheme.Colors()
    let primarySet: OctopusTheme.Colors.ColorSet?
    if primary == 0 && primaryLow == 0 && primaryHigh == 0 {
        primarySet = nil
    } else {
        primarySet = OctopusTheme.Colors.ColorSet(
            main: primary == 0 ? defaults.primary : colorFrom(rgba: primary),
            lowContrast: primaryLow == 0 ? defaults.primaryLowContrast : colorFrom(rgba: primaryLow),
            highContrast: primaryHigh == 0 ? defaults.primaryHighContrast : colorFrom(rgba: primaryHigh)
        )
    }
    return OctopusTheme.Colors(
        primarySet: primarySet,
        onPrimary: optionalColorFrom(rgba: onPrimary),
        link: optionalColorFrom(rgba: link),
        background: optionalColorFrom(rgba: background)
    )
}

@_cdecl("OctopusSdkSetLogo")
public func OctopusSdkSetlogo(logoResourceName: UnsafePointer<Int8>) {
    OctopusSdkClose(keepState: false)
    let path = String(cString: logoResourceName)

    if path.isEmpty {
        logo = nil
        return
    }

    if path.contains("/") {
        let url = URL(fileURLWithPath: path)
        let directory = url.deletingLastPathComponent().path
        let fileName = url.deletingPathExtension().lastPathComponent
        let ext = url.pathExtension.isEmpty ? nil : url.pathExtension

        if let bundlePath = Bundle.main.path(forResource: fileName, ofType: ext, inDirectory: directory) {
            logo = UIImage(contentsOfFile: bundlePath)
        }
    } else {
        logo = UIImage(named: path)
    }
}

@_cdecl("OctopusSdkSetNavBarUsesPrimaryColor")
public func OctopusSdkSetNavBarUsesPrimaryColor(usesPrimary: Bool) {
    OctopusSdkClose(keepState: false)
    navBarUsesPrimaryColor = usesPrimary
}

@_cdecl("OctopusSdkSetColorSchemeType")
public func OctopusSdkSetColorSchemeType(schemeType: Int32) {
    OctopusSdkClose(keepState: false)
    colorSchemeType = schemeType
}

@_cdecl("OctopusSdkSetForcedOrientation")
public func OctopusSdkSetForcedOrientation(orientation: Int32) {
    forcedOrientation = orientation
}

/// Every theme color slot travels as 0 — fully transparent — when the host left it unset.
/// Mapping that to nil is what makes the native default apply.
func optionalColorFrom(rgba: Int32) -> Color? {
    return rgba == 0 ? nil : colorFrom(rgba: rgba)
}

func colorFrom(rgba: Int32) -> Color {
    let alpha = Double((rgba >> 24) & 0xFF) / 255.0
    let red = Double((rgba >> 16) & 0xFF) / 255.0
    let green = Double((rgba >> 8) & 0xFF) / 255.0
    let blue = Double(rgba & 0xFF) / 255.0
    return Color(red: red, green: green, blue: blue, opacity: alpha)
}

private func resolveColorScheme() -> OctopusTheme.Colors {
    let useDark: Bool
    switch colorSchemeType {
    case COLOR_SCHEME_TYPE_LIGHT:
        useDark = false
    case COLOR_SCHEME_TYPE_DARK:
        useDark = true
    default:  // System (0) or any invalid value
        useDark = UITraitCollection.current.userInterfaceStyle == .dark
    }

    if useDark {
        return darkColorScheme ?? OctopusTheme.Colors()
    } else {
        return lightColorScheme ?? OctopusTheme.Colors()
    }
}

@_cdecl("OctopusSdkRegisterNotificationsToken")
public func OctopusSdkRegisterNotificationsToken(token: UnsafePointer<Int8>) {
    let tokenStr = String(cString: token)
    octopus?.set(notificationDeviceToken: tokenStr)
}

@_cdecl("OctopusSdkUpdateNotSeenNotificationsCount")
public func OctopusSdkUpdateNotSeenNotificationsCount() {
    Task{
        try await octopus?.updateNotSeenNotificationsCount()
    }
}

// MARK: - Individual group following (#162)

@_cdecl("OctopusSdkFollowGroup")
public func OctopusSdkFollowGroup(requestId: Int32, groupId: UnsafePointer<Int8>) {
    setGroupFollowing(requestId: requestId, groupId: String(cString: groupId), followed: true)
}

@_cdecl("OctopusSdkUnfollowGroup")
public func OctopusSdkUnfollowGroup(requestId: Int32, groupId: UnsafePointer<Int8>) {
    setGroupFollowing(requestId: requestId, groupId: String(cString: groupId), followed: false)
}

private func groupFollowUnfollowError(_ requestId: Int32, _ type: String, _ message: String) {
    let data = try? JSONSerialization.data(withJSONObject: ["type": type, "message": message])
    let json = data.flatMap { String(data: $0, encoding: .utf8) } ?? "{}"
    sendUnityMessage("OctopusChannel", "OnGroupFollowUnfollowError", "\(requestId)\n\(json)")
}

private func setGroupFollowing(requestId: Int32, groupId: String, followed: Bool) {
    // iOS 1.13.2 has no individual follow API. Match Flutter's one-action adapter.
    Task {
        guard let sdk = octopus else {
            groupFollowUnfollowError(requestId, "unknown", "Call Initialize first")
            return
        }
        do {
            let action = OctopusSyncFollowGroup.Action(groupId: groupId, followed: followed, actionDate: Date())
            let results = try await sdk.syncFollowGroups(actions: [action])
            guard let result = results.first else {
                groupFollowUnfollowError(requestId, "unknown", "Empty syncFollowGroups response")
                return
            }
            switch result.status {
            case .applied, .skipped:
                sendUnityMessage("OctopusChannel", "OnGroupFollowUnfollowResult", "\(requestId)\n")
            case .groupNotFound:
                groupFollowUnfollowError(requestId, "missingGroup", "Group not found")
            case .notFollowable, .notUnfollowable:
                groupFollowUnfollowError(requestId, "unfollowableGroup", "Group follow state cannot be changed")
            case .alreadyFollowed:
                groupFollowUnfollowError(requestId, "groupAlreadyFollowed", "Group is already followed")
            case .alreadyUnfollowed:
                groupFollowUnfollowError(requestId, "groupAlreadyUnfollowed", "Group is already not followed")
            case .unknownError:
                groupFollowUnfollowError(requestId, "unknown", "Unknown server error")
            @unknown default:
                groupFollowUnfollowError(requestId, "unknown", "Unhandled group follow status")
            }
        } catch {
            groupFollowUnfollowError(requestId, "unknown", String(describing: error))
        }
    }
}

// SetReaction uses the same request-id envelope as group operations.
@_cdecl("OctopusSdkSetReaction")
public func OctopusSdkSetReaction(requestId: Int32, contentId: UnsafePointer<Int8>, kind: UnsafePointer<Int8>) {
    // Copy borrowed C strings before entering the asynchronous task.
    let postId = String(cString: contentId)
    let wireKind = String(cString: kind)
    Task { @MainActor in
        let reaction: OctopusReactionKind?
        switch wireKind {
        case "": reaction = nil
        case "heart": reaction = .heart
        case "joy": reaction = .joy
        case "mouthOpen": reaction = .mouthOpen
        case "clap": reaction = .clap
        case "cry": reaction = .cry
        case "rage": reaction = .rage
        default:
            sendSetReactionError(requestId, "unknownReaction", "Unknown reaction not permitted")
            return
        }
        guard let sdk = octopus else {
            sendSetReactionError(requestId, "reactionError", "Call initialize() first")
            return
        }
        do {
            try await sdk.set(reaction: reaction, postId: postId)
            sendUnityMessage("OctopusChannel", "OnSetReactionResult", "\(requestId)\n")
        } catch let error as OctopusSetReactionError {
            // Typed-throws inference does not cross the Task closure: without this
            // explicit cast `error` is `any Error` and the switch does not compile.
            let type: String
            switch error {
            case .unknownReaction: type = "unknownReaction"
            case .postNotFound: type = "postNotFound"
            case .notConnected, .noNetwork, .serverError, .other: type = "reactionError"
            @unknown default: type = "reactionError"
            }
            sendSetReactionError(requestId, type, String(reflecting: error))
        } catch {
            sendSetReactionError(requestId, "reactionError", String(describing: error))
        }
    }
}

private func sendSetReactionError(_ requestId: Int32, _ type: String, _ message: String) {
    let data = try? JSONSerialization.data(withJSONObject: ["type": type, "message": message])
    let json = data.flatMap { String(data: $0, encoding: .utf8) }
        ?? "{\"type\":\"reactionError\",\"message\":\"SetReaction failed\"}"
    sendUnityMessage("OctopusChannel", "OnSetReactionError", "\(requestId)\n\(json)")
}

@_cdecl("OctopusSdkSyncFollowGroups")
public func OctopusSdkSyncFollowGroups(requestId: Int32, actionsJson: UnsafePointer<Int8>) {
    let json = String(cString: actionsJson)
    let actions = parseSyncActions(json)
    Task {
        do {
            let results = try await octopus?.syncFollowGroups(actions: actions) ?? []
            sendUnityMessage("OctopusChannel", "OnSyncFollowGroupsResult", "\(requestId)\n\(syncResultsToJson(results))")
        } catch {
            sendUnityMessage("OctopusChannel", "OnSyncFollowGroupsError", "\(requestId)\n\(String(describing: error))")
        }
    }
}

@_cdecl("OctopusSdkFetchGroups")
public func OctopusSdkFetchGroups(requestId: Int32) {
    Task {
        do {
            try await octopus?.fetchGroups()
            let groups = octopus?.groups ?? []
            sendUnityMessage("OctopusChannel", "OnFetchGroupsResult", "\(requestId)\n\(groupsToJson(groups))")
        } catch {
            sendUnityMessage("OctopusChannel", "OnFetchGroupsError", "\(requestId)\n\(String(describing: error))")
        }
    }
}

private func parseSyncActions(_ json: String) -> [OctopusSyncFollowGroup.Action] {
    guard let data = json.data(using: .utf8),
          let arr = try? JSONSerialization.jsonObject(with: data) as? [[String: Any]] else { return [] }
    return arr.compactMap { o in
        guard let groupId = o["groupId"] as? String else { return nil }
        let followed = (o["followed"] as? Bool) ?? false
        let millis = (o["actionDateMillis"] as? NSNumber)?.doubleValue ?? 0
        return OctopusSyncFollowGroup.Action(
            groupId: groupId, followed: followed,
            actionDate: Date(timeIntervalSince1970: millis / 1000.0))
    }
}

private func syncStatusToWire(_ s: OctopusSyncFollowGroup.Status) -> String {
    switch s {
    case .applied: return "applied"
    case .skipped: return "skipped"
    case .groupNotFound: return "groupNotFound"
    case .notFollowable: return "notFollowable"
    case .notUnfollowable: return "notUnfollowable"
    case .alreadyFollowed: return "alreadyFollowed"
    case .alreadyUnfollowed: return "alreadyUnfollowed"
    // Added by iOS 1.13.0. Same wire token the Kotlin bridge's `else` branch produces.
    case .unknownError: return "unknownError"
    @unknown default: return "unknownError"
    }
}

private func jsonArrayString(_ objects: [[String: Any]]) -> String {
    guard let data = try? JSONSerialization.data(withJSONObject: objects),
          let s = String(data: data, encoding: .utf8) else { return "[]" }
    return s
}

private func syncResultsToJson(_ results: [OctopusSyncFollowGroup.Result]) -> String {
    jsonArrayString(results.map { ["groupId": $0.groupId, "status": syncStatusToWire($0.status)] })
}

private func groupsToJson(_ groups: [OctopusGroup]) -> String {
    jsonArrayString(groups.map {
        ["id": $0.id, "name": $0.name, "isFollowed": $0.isFollowed, "canChangeFollowStatus": $0.canChangeFollowStatus,
         "canAccess": $0.canAccess, "canCreateChildren": $0.canCreateChildren]
    })
}

@_cdecl("OctopusSdkTrackAccessToCommunity")
public func OctopusSdkTrackAccessToCommunity(hasAccess :  Bool) {
    octopus?.track(hasAccessToCommunity: hasAccess)
}

@_cdecl("OctopusSdkOverrideCommunityAccess")
public func OctopusSdkOverrideCommunityAccess(requestId: Int32, hasAccess: Bool) {
    Task {
        do {
            try await octopus?.overrideCommunityAccess(hasAccess)
            // Success carries no data; the new access value flows back via the
            // $hasAccessToCommunity subscription (OnHasAccessToCommunity).
            sendUnityMessage("OctopusChannel", "OnOverrideCommunityAccessResult", "\(requestId)\n")
        } catch {
            sendUnityMessage("OctopusChannel", "OnOverrideCommunityAccessError", "\(requestId)\n\(String(describing: error))")
        }
    }
}

@_cdecl("OctopusSdkTrack")
public func OctopusSdkTrack(
    name : UnsafePointer<CChar>?,
    keys: UnsafePointer<UnsafePointer<CChar>?>?,
    values: UnsafePointer<UnsafePointer<CChar>?>?,
    count: Int32
) {
    guard let name = name else { return }
    guard let keys = keys else { return }
    guard let values = values else { return }
    
    let nameStr = String(cString: name)
    var props: [String: Octopus.CustomEvent.PropertyValue] = [:]
    
    for i in 0..<Int(count) {
        let key = String(cString: keys[i]!)
        let value = String(cString: values[i]!)
        props[key] = .init(value: value)
    }
    
    Task {
        try await octopus?.track(customEvent: Octopus.CustomEvent(
            name: nameStr,
            properties: props
        ))
    }
}

@_cdecl("OctopusSdkSetFonts")
public func OctopusSdkSetFonts(
    title1Font: UnsafePointer<Int8>, title1Size: Float,
    title2Font: UnsafePointer<Int8>, title2Size: Float,
    body1Font: UnsafePointer<Int8>, body1Size: Float,
    body2Font: UnsafePointer<Int8>, body2Size: Float,
    caption1Font: UnsafePointer<Int8>, caption1Size: Float,
    caption2Font: UnsafePointer<Int8>, caption2Size: Float,
    navBarItemFont: UnsafePointer<Int8>, navBarItemSize: Float
) {
    OctopusSdkClose(keepState: false)

    let title1Name = String(cString: title1Font)
    let title2Name = String(cString: title2Font)
    let body1Name = String(cString: body1Font)
    let body2Name = String(cString: body2Font)
    let caption1Name = String(cString: caption1Font)
    let caption2Name = String(cString: caption2Font)
    let navBarItemName = String(cString: navBarItemFont)

    let allEmpty = title1Name.isEmpty && title2Name.isEmpty && body1Name.isEmpty &&
                   body2Name.isEmpty && caption1Name.isEmpty && caption2Name.isEmpty &&
                   navBarItemName.isEmpty

    if allEmpty {
        fonts = nil
        return
    }

    let defaultFonts = OctopusTheme.Fonts()

    fonts = OctopusTheme.Fonts(
        title1: fontFrom(name: title1Name, size: title1Size) ?? defaultFonts.title1,
        title2: fontFrom(name: title2Name, size: title2Size) ?? defaultFonts.title2,
        body1: fontFrom(name: body1Name, size: body1Size) ?? defaultFonts.body1,
        body2: fontFrom(name: body2Name, size: body2Size) ?? defaultFonts.body2,
        caption1: fontFrom(name: caption1Name, size: caption1Size) ?? defaultFonts.caption1,
        caption2: fontFrom(name: caption2Name, size: caption2Size) ?? defaultFonts.caption2,
        navBarItem: fontFrom(name: navBarItemName, size: navBarItemSize) ?? defaultFonts.navBarItem
    )
}

// Applied after OctopusSdkSetFonts has restored the name/size defaults.
@_cdecl("OctopusSdkSetFontWeights")
public func OctopusSdkSetFontWeights(json: UnsafePointer<Int8>) {
    guard let data = String(cString: json).data(using: .utf8),
          let rows = try? JSONSerialization.jsonObject(with: data) as? [[String: Any]],
          !rows.isEmpty else { return }
    var weights: [String: Font.Weight] = [:]
    for row in rows {
        guard let slot = row["slot"] as? String,
              let value = row["fontWeight"] as? Int else { continue }
        weights[slot] = fontWeightFrom(value)
    }
    let base = fonts ?? OctopusTheme.Fonts()
    func weighted(_ slot: String, _ font: Font) -> Font {
        guard let weight = weights[slot] else { return font }
        return font.weight(weight)
    }
    fonts = OctopusTheme.Fonts(
        title1: weighted("title1", base.title1),
        title2: weighted("title2", base.title2),
        body1: weighted("body1", base.body1),
        body2: weighted("body2", base.body2),
        caption1: weighted("caption1", base.caption1),
        caption2: weighted("caption2", base.caption2),
        navBarItem: weighted("navBarItem", base.navBarItem)
    )
}

// Same nearest named-weight buckets as Flutter and React Native.
private func fontWeightFrom(_ value: Int) -> Font.Weight {
    switch value {
    case ..<150: return .ultraLight
    case ..<250: return .thin
    case ..<350: return .light
    case ..<450: return .regular
    case ..<550: return .medium
    case ..<650: return .semibold
    case ..<750: return .bold
    case ..<850: return .heavy
    default: return .black
    }
}

private func fontFrom(name: String, size: Float) -> Font? {
    if name.isEmpty || size <= 0 {
        return nil
    }
    return Font.custom(name, size: CGFloat(size))
}

// MARK: - Event serialization

/// Maps an OctopusEvent to a flat JSON string with the same canonical field names and
/// token values as the Android Bridge.eventToJson(). Both feeds the same C# parser.
private func eventToJson(_ e: OctopusEvent) -> String {
    var o: [String: Any] = [:]
    switch e {

    // --- Content creation ---
    case .postCreated(let ctx):
        o["type"] = "PostCreated"
        o["postId"] = ctx.postId
        o["groupId"] = ctx.groupId
        o["textLength"] = String(ctx.textLength)
        var parts: [String] = []
        if ctx.content.contains(.text)  { parts.append("Text") }
        if ctx.content.contains(.image) { parts.append("Image") }
        if ctx.content.contains(.poll)  { parts.append("Poll") }
        o["content"] = parts.joined(separator: ",")

    case .commentCreated(let ctx):
        o["type"] = "CommentCreated"
        o["commentId"] = ctx.commentId
        o["postId"] = ctx.postId
        o["textLength"] = String(ctx.textLength)

    case .replyCreated(let ctx):
        o["type"] = "ReplyCreated"
        o["replyId"] = ctx.replyId
        o["commentId"] = ctx.commentId
        o["textLength"] = String(ctx.textLength)

    // --- Deletion (iOS has a unified contentDeleted, no parentId) ---
    case .contentDeleted(let ctx):
        o["type"] = "ContentDeleted"
        o["contentId"] = ctx.contentId
        o["contentKind"] = contentKindToken(ctx.kind)

    // --- Reaction ---
    case .reactionModified(let ctx):
        o["type"] = "ReactionModified"
        o["contentId"] = ctx.contentId
        o["contentKind"] = contentKindToken(ctx.contentKind)
        if let prev = reactionKindToken(ctx.previousReaction) { o["previousReaction"] = prev }
        if let next = reactionKindToken(ctx.newReaction)      { o["newReaction"] = next }

    // --- Poll ---
    case .pollVoted(let ctx):
        o["type"] = "PollVoted"
        o["contentId"] = ctx.contentId
        o["optionId"] = ctx.optionId

    // --- Reporting ---
    case .contentReported(let ctx):
        o["type"] = "ContentReported"
        o["contentId"] = ctx.contentId
        // iOS ContentReportedContext has no contentKind field — omitted
        o["reasons"] = ctx.reasons.map { reportReasonToken($0) }.joined(separator: ",")

    // --- Group ---
    case .groupFollowingChanged(let ctx):
        o["type"] = "GroupFollowingChanged"
        o["groupId"] = ctx.groupId
        o["followed"] = ctx.followed

    // --- Gamification ---
    case .gamificationPointsGained(let ctx):
        o["type"] = "GamificationPointsGained"
        o["points"] = String(ctx.pointsGained)
        o["action"] = gamificationGainedActionToken(ctx.action)

    case .gamificationPointsRemoved(let ctx):
        o["type"] = "GamificationPointsRemoved"
        o["points"] = String(ctx.pointsRemoved)
        o["action"] = gamificationRemovedActionToken(ctx.action)

    // --- Screens ---
    case .screenDisplayed(let ctx):
        o["type"] = "ScreenDisplayed"
        switch ctx.screen {
        case .mainFeed(let s):
            o["screen"] = "MainFeed"
            o["feedId"] = s.feedId
        case .postsFeed(let s):
            o["screen"] = "PostsFeed"
            o["feedId"] = s.feedId
        case .postDetail(let s):
            o["screen"] = "PostDetail"
            o["postId"] = s.postId
        case .commentDetail(let s):
            o["screen"] = "CommentDetail"
            o["commentId"] = s.commentId
        case .groups:
            o["screen"] = "Groups"
        case .groupDetail(let s):
            o["screen"] = "GroupDetail"
            o["groupId"] = s.groupId
            o["source"] = s.source == .clientApp ? "ClientApp" : "Community"
        case .createPost:
            o["screen"] = "CreatePost"
        case .profile:
            o["screen"] = "Profile"
        case .otherUserProfile(let s):
            o["screen"] = "OtherUserProfile"
            o["profileId"] = s.profileId
        case .otherUserPosts(let s):
            o["screen"] = "OtherUserPosts"
            o["profileId"] = s.profileId
        case .editProfile:
            o["screen"] = "EditProfile"
        case .reportContent:
            o["screen"] = "ReportContent"
        case .reportProfile:
            o["screen"] = "ReportProfile"
        case .validateNickname:
            o["screen"] = "ValidateNickname"
        case .settingsList:
            o["screen"] = "SettingsList"
        case .settingsAccount:
            o["screen"] = "SettingsAccount"
        case .reportExplanation:
            o["screen"] = "ReportExplanation"
        case .deleteAccount:
            o["screen"] = "DeleteAccount"
        @unknown default:
            o["screen"] = "Unknown"
        }

    // --- Notification / clicks ---
    case .notificationClicked(let ctx):
        o["type"] = "NotificationClicked"
        o["notificationId"] = ctx.notificationId
        if let cid = ctx.contentId { o["contentId"] = cid }

    case .postClicked(let ctx):
        o["type"] = "PostClicked"
        o["postId"] = ctx.postId
        o["source"] = ctx.source == .feed ? "Feed" : "Profile"

    case .translationButtonClicked(let ctx):
        o["type"] = "TranslationButtonClicked"
        o["contentId"] = ctx.contentId
        o["viewTranslated"] = ctx.viewTranslated
        o["contentKind"] = contentKindToken(ctx.contentKind)

    case .commentButtonClicked(let ctx):
        o["type"] = "CommentButtonClicked"
        o["postId"] = ctx.postId

    case .replyButtonClicked(let ctx):
        o["type"] = "ReplyButtonClicked"
        o["commentId"] = ctx.commentId

    case .seeRepliesButtonClicked(let ctx):
        o["type"] = "SeeRepliesButtonClicked"
        o["commentId"] = ctx.commentId

    // --- Profile modification ---
    case .profileModified(let ctx):
        o["type"] = "ProfileModified"
        o["nicknameChanged"] = ctx.nickname.isUpdated
        o["bioChanged"] = ctx.bio.isUpdated
        o["pictureChanged"] = ctx.picture.isUpdated
        if case .updated(let bioCtx) = ctx.bio {
            o["bioLength"] = String(bioCtx.bioLength)
        } else {
            o["bioLength"] = "0"
        }
        if case .updated(let picCtx) = ctx.picture {
            o["hasPicture"] = picCtx.hasPicture
        } else {
            o["hasPicture"] = false
        }

    // --- Sessions ---
    case .sessionStarted(let ctx):
        o["type"] = "SessionStarted"
        o["sessionId"] = ctx.sessionId

    case .sessionStopped(let ctx):
        o["type"] = "SessionStopped"
        o["sessionId"] = ctx.sessionId
    }

    guard let data = try? JSONSerialization.data(withJSONObject: o),
          let s = String(data: data, encoding: .utf8) else { return "{}" }
    return s
}

private func contentKindToken(_ k: OctopusEvent.ContentKind) -> String {
    switch k {
    case .post:    return "Post"
    case .comment: return "Comment"
    case .reply:   return "Reply"
    }
}

private func reactionKindToken(_ k: OctopusEvent.ReactionKind?) -> String? {
    guard let k else { return nil }
    switch k {
    case .heart:        return "Heart"
    case .joy:          return "Joy"
    case .mouthOpen:    return "MouthOpen"
    case .clap:         return "Clap"
    case .cry:          return "Cry"
    case .rage:         return "Rage"
    case .unknown:      return "Unknown"
    }
}

private func reportReasonToken(_ r: OctopusEvent.ReportReason) -> String {
    switch r {
    case .hateSpeechOrDiscriminationOrHarassment: return "hateSpeechOrDiscriminationOrHarassment"
    case .explicitOrInappropriateContent:         return "explicitOrInappropriateContent"
    case .violenceAndTerrorism:                   return "violenceAndTerrorism"
    case .spamAndScams:                           return "spamAndScams"
    case .suicideAndSelfHarm:                     return "suicideAndSelfHarm"
    case .fakeProfilesAndImpersonation:           return "fakeProfilesAndImpersonation"
    case .childExploitationOrAbuse:               return "childExploitationOrAbuse"
    case .intellectualPropertyViolation:          return "intellectualPropertyViolation"
    case .other:                                  return "other"
    }
}

private func gamificationGainedActionToken(_ a: OctopusEvent.GamificationPointsGainedAction) -> String {
    switch a {
    case .reaction:         return "Reaction"
    case .comment:          return "Comment"
    case .reply:            return "Reply"
    case .post:             return "Post"
    case .vote:             return "Vote"
    case .postCommented:    return "PostCommented"
    case .profileCompleted: return "ProfileCompleted"
    case .dailySession:     return "DailySession"
    }
}

private func gamificationRemovedActionToken(_ a: OctopusEvent.GamificationPointsRemovedAction) -> String {
    switch a {
    case .postDeleted:     return "Post"
    case .commentDeleted:  return "Comment"
    case .replyDeleted:    return "Reply"
    case .reactionDeleted: return "Reaction"
    }
}

// MARK: - Connected profile and entitlements

private func profileToJson(_ profile: OctopusProfile?) -> String {
    guard let profile else { return "null" }
    let object: [String: Any] = [
        "entitlements": Array(profile.entitlements),
        "clientUserId": profile.clientUserId as Any? ?? NSNull()
    ]
    guard let data = try? JSONSerialization.data(withJSONObject: object),
          let json = String(data: data, encoding: .utf8) else { return "null" }
    return json
}

@_cdecl("OctopusSdkRefreshEntitlements")
public func OctopusSdkRefreshEntitlements(requestId: Int32) {
    guard let sdk = octopus else {
        sendRefreshEntitlementsError(requestId, "userNotConnected", "SDK not initialized")
        return
    }
    Task {
        do {
            try await sdk.refreshEntitlements()
            sendUnityMessage("OctopusChannel", "OnRefreshEntitlementsResult", "\(requestId)\n")
        } catch let error as OctopusRefreshEntitlementsError {
            // Typed-throws inference does not cross the Task closure: without this
            // explicit cast `error` is `any Error` and the switch does not compile.
            switch error {
            case .noClientTokenProvider:
                sendRefreshEntitlementsError(requestId, "noClientTokenProvider", "No client token provider registered")
            case .userNotConnected:
                sendRefreshEntitlementsError(requestId, "userNotConnected", "No connected user")
            case .noNetwork:
                sendRefreshEntitlementsError(requestId, "noNetwork", "No network")
            case .userBanned(let message):
                sendRefreshEntitlementsError(requestId, "userBanned", message)
            case .serverError(let underlying):
                sendRefreshEntitlementsError(requestId, "serverError", String(describing: underlying))
            }
        } catch {
            sendRefreshEntitlementsError(requestId, "serverError", String(describing: error))
        }
    }
}

private func sendRefreshEntitlementsError(_ requestId: Int32, _ type: String, _ message: String) {
    let object = ["type": type, "message": message]
    let data = try? JSONSerialization.data(withJSONObject: object)
    let json = data.flatMap { String(data: $0, encoding: .utf8) } ?? "{}"
    sendUnityMessage("OctopusChannel", "OnRefreshEntitlementsError", "\(requestId)\n\(json)")
}

// MARK: - Debug/QA configuration (#165)
// Overrides are synchronous. Unity's main thread is normally the iOS main thread;
// marshal other callers synchronously to satisfy the native SDK's MainActor contract.
private func applyDebugConfigOverride(_ action: @escaping @MainActor () -> Void) {
    if Thread.isMainThread {
        MainActor.assumeIsolated { action() }
    } else {
        DispatchQueue.main.sync { action() }
    }
}

private func debugConfigObject(_ pointer: UnsafePointer<CChar>) -> [String: Any]? {
    let json = String(cString: pointer)
    guard json != "null", let data = json.data(using: .utf8) else { return nil }
    return (try? JSONSerialization.jsonObject(with: data)) as? [String: Any]
}

private func debugProfileFieldLock(_ value: Any?) -> ProfileFieldLockState? {
    switch value as? String {
    case "editable": return .editable
    case "readOnly": return .readOnly
    case "disabled": return .disabled
    default: return nil
    }
}

@_cdecl("OctopusSdkDebugOverrideProfileFieldsLock")
public func OctopusSdkDebugOverrideProfileFieldsLock(json: UnsafePointer<CChar>) {
    let object = debugConfigObject(json)
    var lock: ProfileFieldsLock?
    if let object = object {
        guard let nickname = debugProfileFieldLock(object["nickname"]),
              let avatar = debugProfileFieldLock(object["avatar"]),
              let bio = debugProfileFieldLock(object["bio"]) else { return }
        lock = ProfileFieldsLock(nickname: nickname, avatar: avatar, bio: bio)
    }
    let mapped = lock
    applyDebugConfigOverride { octopus?.debugOverrideProfileFieldsLock(mapped) }
}

@_cdecl("OctopusSdkDebugOverrideContentOptions")
public func OctopusSdkDebugOverrideContentOptions(json: UnsafePointer<CChar>) {
    let object = debugConfigObject(json)
    let options = object.map {
        ContentOptions(
            post: ContentOptions.PostOptions(
                enablePictures: $0["postEnablePictures"] as? Bool ?? true,
                enablePolls: $0["postEnablePolls"] as? Bool ?? true),
            comment: ContentOptions.CommentOptions(enablePictures: $0["commentEnablePictures"] as? Bool ?? true),
            reply: ContentOptions.ReplyOptions(enablePictures: $0["replyEnablePictures"] as? Bool ?? true))
    }
    applyDebugConfigOverride { octopus?.debugOverrideContentOptions(options) }
}

@_cdecl("OctopusSdkDebugOverrideTermsAcceptanceMode")
public func OctopusSdkDebugOverrideTermsAcceptanceMode(mode: UnsafePointer<CChar>) {
    let mapped: TermsAcceptanceMode?
    switch String(cString: mode) {
    case "null": mapped = nil
    case "implicit": mapped = .implicit
    case "explicitMultiCheckbox": mapped = .explicitMultiCheckbox
    case "explicitSingleCheckbox": mapped = .explicitSingleCheckbox
    default: return
    }
    applyDebugConfigOverride { octopus?.debugOverrideTermsAcceptanceMode(mapped) }
}

@_cdecl("OctopusSdkDebugOverrideExposeClientUserId")
public func OctopusSdkDebugOverrideExposeClientUserId(enabled: UnsafePointer<CChar>) {
    let mapped: Bool?
    switch String(cString: enabled) {
    case "null": mapped = nil
    case "true": mapped = true
    case "false": mapped = false
    default: return
    }
    applyDebugConfigOverride { octopus?.debugOverrideExposeClientUserId(mapped) }
}

@_cdecl("OctopusSdkDebugGetCommunityConfig")
public func OctopusSdkDebugGetCommunityConfig(requestId: Int32) {
    Task { @MainActor in
        guard let sdk = octopus else {
            sendUnityMessage("OctopusChannel", "OnDebugGetCommunityConfigError", "\(requestId)\nSDK not initialized")
            return
        }
        // Same debug-only inspection as React Native. `core` is package-visible,
        // so reflection is deliberately confined to this diagnostic entry point.
        guard let core = Mirror(reflecting: sdk).children.first(where: { $0.label == "core" })?.value as? OctopusSDKCore else {
            sendUnityMessage("OctopusChannel", "OnDebugGetCommunityConfigError", "\(requestId)\nCould not reach the SDK core")
            return
        }
        guard let config = core.configRepository.communityConfig else {
            sendUnityMessage("OctopusChannel", "OnDebugGetCommunityConfigResult", "\(requestId)\nnull")
            return
        }
        do {
            let row: [String: Any] = [
                "exposeClientUserId": config.exposeClientUserId,
                "forceLoginOnStrongActions": config.forceLoginOnStrongActions,
                "displayAccountAge": config.displayAccountAge,
                "termsAcceptanceMode": String(describing: config.termsAcceptanceMode)
            ]
            let data = try JSONSerialization.data(withJSONObject: row)
            let json = String(decoding: data, as: UTF8.self)
            sendUnityMessage("OctopusChannel", "OnDebugGetCommunityConfigResult", "\(requestId)\n\(json)")
        } catch {
            sendUnityMessage("OctopusChannel", "OnDebugGetCommunityConfigError", "\(requestId)\n\(error)")
        }
    }
}
// MARK: - Client-object related posts (#166)

private var clientPostCancellables: [String: AnyCancellable] = [:]
private var clientPostObservationIds: [String: UUID] = [:]
private var clientPostTasks: [Int32: Task<Void, Never>] = [:]
private var clientPostSignContinuations: [Int32: CheckedContinuation<String, Error>] = [:]
private var clientPostSignCallback: BridgeShareSignCallback?

private func clearClientPostSession() {
    clientPostObservationIds.removeAll()
    clientPostCancellables.values.forEach { $0.cancel() }
    clientPostCancellables.removeAll()
    clientPostTasks.values.forEach { $0.cancel() }
    clientPostTasks.removeAll()
    let pending = clientPostSignContinuations.values
    clientPostSignContinuations.removeAll()
    pending.forEach { $0.resume(throwing: CancellationError()) }
}

@_cdecl("OctopusSdkSetClientPostSignCallback")
public func OctopusSdkSetClientPostSignCallback(callback: BridgeShareSignCallback?) {
    clientPostSignCallback = callback
}

@_cdecl("OctopusSdkSetClientPostSignature")
public func OctopusSdkSetClientPostSignature(requestId: Int32, signature: UnsafePointer<CChar>) {
    let value = String(cString: signature)
    DispatchQueue.main.async {
        guard let pending = clientPostSignContinuations.removeValue(forKey: requestId) else { return }
        if value.isEmpty { pending.resume(throwing: BridgeShareSignError.hostFailed) }
        else { pending.resume(returning: value) }
    }
}

@MainActor
private func requestClientPostSignature(_ requestId: Int32, _ fingerprint: String) async throws -> String {
    try Task.checkCancellation()
    guard let callback = clientPostSignCallback else { throw BridgeShareSignError.noCallback }
    return try await withCheckedThrowingContinuation { continuation in
        clientPostSignContinuations[requestId] = continuation
        "\(requestId)\n\(fingerprint)".withCString { callback($0) }
    }
}

@_cdecl("OctopusSdkFetchOrCreateClientObjectRelatedPost")
public func OctopusSdkFetchOrCreateClientObjectRelatedPost(
    requestId: Int32, json: UnsafePointer<CChar>, hasSigner: Int32
) {
    let payload = String(cString: json)
    clientPostTasks[requestId] = Task { @MainActor in
        defer { clientPostTasks.removeValue(forKey: requestId) }
        guard let sdk = octopus else {
            sendClientPostError(requestId, "other", "Call Initialize first")
            return
        }
        do {
            guard let data = payload.data(using: .utf8),
                  let row = try JSONSerialization.jsonObject(with: data) as? [String: String],
                  let objectId = row["objectId"], !objectId.isEmpty else {
                sendClientPostError(requestId, "missingObjectId", "An object id is required")
                return
            }
            func optional(_ key: String) -> String? {
                guard let value = row[key], !value.isEmpty else { return nil }
                return value
            }
            var attachment: Octopus.ClientPost.Attachment?
            if let path = optional("imagePath") {
                let bytes = try await Task.detached { try Data(contentsOf: URL(fileURLWithPath: path)) }.value
                attachment = .localImage(bytes)
            } else if let remote = optional("imageUrl") {
                guard let url = URL(string: remote), let scheme = url.scheme?.lowercased(),
                      ["https", "http"].contains(scheme), url.host != nil else {
                    sendClientPostError(requestId, "fileDownload", "An absolute HTTP(S) image URL is required")
                    return
                }
                attachment = .distantImage(url)
            }
            try Task.checkCancellation()
            let content = Octopus.ClientPost(clientObjectId: objectId, groupId: optional("groupId"),
                text: row["text"] ?? "", catchPhrase: optional("catchPhrase"), attachment: attachment,
                viewClientObjectButtonText: optional("viewObjectButtonText"))
            let signer: @Sendable (String) async throws -> String? = { fingerprint in
                if hasSigner == 0 { return nil }
                return try await requestClientPostSignature(requestId, fingerprint)
            }
            let post = try await sdk.fetchOrCreateClientObjectRelatedPost(content: content, tokenProvider: signer)
            if !Task.isCancelled {
                sendUnityMessage("OctopusChannel", "OnClientPostResult", "\(requestId)\n\(post.id)")
            }
        } catch let error as ClientPostError {
            if !Task.isCancelled {
                // ValidationError's fields are internal in 1.13.2. Do not infer kinds from descriptions.
                switch error {
                case .validation, .noNetwork, .serverError, .other:
                    sendClientPostError(requestId, "other", error.debugDescription)
                }
            }
        } catch {
            if !Task.isCancelled { sendClientPostError(requestId, "other", error.localizedDescription) }
        }
    }
}

@_cdecl("OctopusSdkStartObservingClientObjectRelatedPost")
public func OctopusSdkStartObservingClientObjectRelatedPost(objectId: UnsafePointer<CChar>, generation: Int32) {
    let id = String(cString: objectId)
    DispatchQueue.main.async {
        clientPostCancellables.removeValue(forKey: id)?.cancel()
        guard let sdk = octopus else {
            print("[Octopus SDK] Client post observation requires Initialize")
            return
        }
        let observationId = UUID()
        clientPostObservationIds[id] = observationId
        sendUnityMessage("OctopusChannel", "OnClientPostObservationStarted", "\(id)\n\(generation)")
        clientPostCancellables[id] = sdk.getClientObjectRelatedPostPublisher(clientObjectId: id)
            .receive(on: DispatchQueue.main)
            .sink { post in
                guard clientPostObservationIds[id] == observationId else { return }
                sendUnityMessage("OctopusChannel", "OnClientObjectRelatedPostChanged",
                    "\(id)\n\(post.map { clientPostToJson($0) } ?? "null")")
            }
    }
}

@_cdecl("OctopusSdkStopObservingClientObjectRelatedPost")
public func OctopusSdkStopObservingClientObjectRelatedPost(objectId: UnsafePointer<CChar>) {
    let id = String(cString: objectId)
    DispatchQueue.main.async {
        clientPostObservationIds.removeValue(forKey: id)
        clientPostCancellables.removeValue(forKey: id)?.cancel()
    }
}

private func clientPostToJson(_ post: any OctopusPost) -> String {
    let row: [String: Any] = [
        "id": post.id, "commentCount": post.commentCount, "viewCount": post.viewCount,
        "reactions": post.reactions.map { ["reactionKind": clientPostReactionToken($0.reaction), "count": $0.count] as [String: Any] },
        "userReactionKind": post.userReaction.map { clientPostReactionToken($0) } as Any? ?? NSNull()
    ]
    guard let data = try? JSONSerialization.data(withJSONObject: row),
          let json = String(data: data, encoding: .utf8) else { return "null" }
    return json
}

private func clientPostReactionToken(_ kind: OctopusReactionKind) -> String {
    switch kind {
    case .heart: return "Heart"
    case .joy: return "Joy"
    case .mouthOpen: return "MouthOpen"
    case .clap: return "Clap"
    case .cry: return "Cry"
    case .rage: return "Rage"
    case .unknown: return "Unknown"
    }
}

private func sendClientPostError(_ requestId: Int32, _ type: String, _ message: String) {
    let row = ["type": type, "message": message]
    guard let data = try? JSONSerialization.data(withJSONObject: row),
          let json = String(data: data, encoding: .utf8) else { return }
    sendUnityMessage("OctopusChannel", "OnClientPostError", "\(requestId)\n\(json)")
}
