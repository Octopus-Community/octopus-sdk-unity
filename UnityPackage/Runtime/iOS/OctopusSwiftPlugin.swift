import Foundation
import Octopus
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
var eventsCancellable: AnyCancellable?

// Toggled from C# (OctopusSdkSetUrlInterceptionEnabled). When false the SDK opens URLs
// itself (in-app browser); when true taps are forwarded to Unity for the host to decide.
private var urlInterceptionEnabled = false

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
    @Binding var notificationUserInfo: [AnyHashable: Any]?
    let theme: OctopusTheme

    var body: some View {
        OctopusHomeScreen(
            octopus: octopus,
            mainFeedNavBarTitle: navBarTitle,
            mainFeedColoredNavBar: coloredNavBar,
            initialScreen: initialScreen,
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
        octopus = try OctopusSDK(apiKey: key, connectionMode: connMode, configuration: configuration)
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
        // Unity's Bundle.main doesn't include .lproj folders for all languages,
        // so the SDK's default language detection (Bundle.main.preferredLocalizations)
        // returns "en" regardless of the device language.
        // Override with the actual device locale so gRPC Accept-Language header is correct.
        octopus?.overrideDefaultLocale(with: Locale.current)
        notSeenNotifCancellable = octopus?.$notSeenNotificationsCount.sink { count in
            sendUnityMessage("OctopusChannel", "OnNotSeenNotificationsCount", String(count) )
        }
        groupsCancellable = octopus?.$groups.sink { groups in
            sendUnityMessage("OctopusChannel", "OnGroupsChanged", groupsToJson(groups))
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
    } catch {
        print("Octopus Init Error: \(error)")
    }
}

private func parseConnectionMode(
    _ connectionMode: UnsafePointer<Int8>, _ fieldsPtr: UnsafePointer<Int32>, _ fieldsCount: Int32
) -> ConnectionMode {
    let deepLink: String? = nil
    let mode = String(cString: connectionMode)
    let fieldsBuf = UnsafeBufferPointer(
        start: fieldsPtr,
        count: Int(fieldsCount)
    )
    let fields: Set<ConnectionMode.SSOConfiguration.ProfileField> =
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

private func fieldToString(_ profileField: ConnectionMode.SSOConfiguration.ProfileField?) -> String?
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
public func OctopusSdkOpen(payloadJson: UnsafePointer<Int8>) {
    let json = String(cString: payloadJson)
    presentHome(initialScreen: .mainFeed, payloadJson: json.isEmpty ? nil : json)
}

@_cdecl("OctopusSdkOpenGroup")
public func OctopusSdkOpenGroup(groupId: UnsafePointer<Int8>) {
    let gid = String(cString: groupId)
    presentHome(initialScreen: gid.isEmpty ? .mainFeed : .group(.init(groupId: gid)), payloadJson: nil)
}

@_cdecl("OctopusSdkOpenPost")
public func OctopusSdkOpenPost(postId: UnsafePointer<Int8>) {
    let pid = String(cString: postId)
    presentHome(initialScreen: pid.isEmpty ? .mainFeed : .post(.init(postId: pid)), payloadJson: nil)
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
    ctaLabel: UnsafePointer<Int8>, ctaUrl: UnsafePointer<Int8>, hasSigner: Int32
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

        // OctopusPrefilledPost.init throws unless text or image is provided, so a
        // blank (or topic-only) request opens the empty editor via prefilledPost: nil.
        let info: OctopusInitialScreen.CreatePostScreenInfo
        if textStr.isEmpty && imageData == nil {
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
        presentHome(initialScreen: .createPost(info), payloadJson: nil)
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

private func presentHome(initialScreen: OctopusInitialScreen, payloadJson: String?) {
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
        let wantsMainFeed = (userInfo == nil) && isMainFeed(initialScreen)
        if !wantsMainFeed || !octopusControllerShowsMainFeed {
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
            octopusController = OctopusHostingController(rootView: AnyView(root))
            octopusController?.modalPresentationStyle = .fullScreen
            octopusControllerShowsMainFeed = isMainFeed(effectiveScreen)
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
    primary: Int32, primaryLow: Int32, primaryHigh: Int32, onPrimary: Int32
) {
    OctopusSdkClose(keepState: false)
    lightColorScheme = OctopusTheme.Colors(
        primarySet: OctopusTheme.Colors.ColorSet(
            main: colorFrom(rgba: primary),
            lowContrast: colorFrom(rgba: primaryLow),
            highContrast: colorFrom(rgba: primaryHigh)
        ),
        onPrimary: colorFrom(rgba: onPrimary)
    )
}

@_cdecl("OctopusSdkSetDarkColorScheme")
public func OctopusSdkSetDarkColorScheme(
    primary: Int32, primaryLow: Int32, primaryHigh: Int32, onPrimary: Int32
) {
    OctopusSdkClose(keepState: false)
    darkColorScheme = OctopusTheme.Colors(
        primarySet: OctopusTheme.Colors.ColorSet(
            main: colorFrom(rgba: primary),
            lowContrast: colorFrom(rgba: primaryLow),
            highContrast: colorFrom(rgba: primaryHigh)
        ),
        onPrimary: colorFrom(rgba: onPrimary)
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
        ["id": $0.id, "name": $0.name, "isFollowed": $0.isFollowed, "canChangeFollowStatus": $0.canChangeFollowStatus]
    })
}

@_cdecl("OctopusSdkTrackAccessToCommunity")
public func OctopusSdkTrackAccessToCommunity(hasAccess :  Bool) {
    octopus?.track(hasAccessToCommunity: hasAccess)
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
    var props: [String: CustomEvent.PropertyValue] = [:]
    
    for i in 0..<Int(count) {
        let key = String(cString: keys[i]!)
        let value = String(cString: values[i]!)
        props[key] = .init(value: value)
    }
    
    Task {
        try await octopus?.track(customEvent: CustomEvent(
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
        case .settingsAbout:
            o["screen"] = "SettingsAbout"
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
