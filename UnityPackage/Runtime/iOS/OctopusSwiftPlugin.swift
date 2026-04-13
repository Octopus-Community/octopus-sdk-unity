import Foundation
import Octopus
import OctopusUI
import SwiftUI
import UIKit
import Combine
import UserNotifications

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

private var octopusController: UIHostingController<AnyView>?
var notSeenNotifCancellable: AnyCancellable?
private var sdkInitialized = false

// MARK: - Push Notification State & Helper

/// Holds the pending UNNotificationResponse captured at the native level when the user taps
/// an Octopus push notification. The response is passed as a binding to OctopusHomeScreen
/// so the native SDK can navigate to the correct screen (post, comment, reply).
class OctopusNotificationState: ObservableObject {
    static let shared = OctopusNotificationState()
    @Published var pendingNotificationResponse: UNNotificationResponse?
    private init() {}
}

/// Helper callable from Objective-C (via the auto-generated UnityFramework-Swift.h header).
/// Developers call these methods from their UnityAppController subclass to forward
/// notification responses to the Octopus SDK.
@objc public class OctopusNotificationHelper: NSObject {
    /// Call from userNotificationCenter:didReceiveNotificationResponse:withCompletionHandler:.
    /// If the notification is from Octopus, stores the response so OctopusHomeScreen
    /// can navigate to the correct screen when Open() is called.
    @objc public static func handleNotificationResponse(_ response: UNNotificationResponse) {
        if OctopusSDK.isAnOctopusNotification(notification: response.notification) {
            let work = {
                OctopusNotificationState.shared.pendingNotificationResponse = response
                if sdkInitialized {
                    // Warm/background: OctopusChannel already exists, notify C# directly.
                    sendUnityMessage("OctopusChannel", "OnNotificationTapped", "")
                }
                // Cold start: sdkInitialized is false. OctopusSdkInitialize() will detect
                // the stored response and fire OnNotificationTapped itself.
            }
            if Thread.isMainThread {
                work()
            } else {
                DispatchQueue.main.async { work() }
            }
        }
    }

    /// Returns true if the notification was sent by the Octopus platform.
    @objc public static func isOctopusNotification(_ notification: UNNotification) -> Bool {
        return OctopusSDK.isAnOctopusNotification(notification: notification)
    }
}

// MARK: - Bridge Root View

/// Wrapper view that connects OctopusNotificationState to OctopusHomeScreen's notificationResponse binding.
private struct OctopusBridgeRootView: View {
    @ObservedObject var notificationState = OctopusNotificationState.shared
    let octopus: OctopusSDK
    let navBarTitle: OctopusMainFeedTitle?
    let coloredNavBar: Bool
    let postId: String?
    let theme: OctopusTheme

    var body: some View {
        OctopusHomeScreen(
            octopus: octopus,
            mainFeedNavBarTitle: navBarTitle,
            mainFeedColoredNavBar: coloredNavBar,
            postId: postId,
            notificationResponse: $notificationState.pendingNotificationResponse
        )
        .environment(\.octopusTheme, theme)
    }
}

@_cdecl("OctopusSdkInitialize")
public func OctopusSdkInitialize(
    apiKey: UnsafePointer<Int8>, connectionMode: UnsafePointer<Int8>,
    appManagedFields: UnsafePointer<Int32>, appManagedFieldsCount: Int32
) {
    let key = String(cString: apiKey)
    let connMode = parseConnectionMode(connectionMode, appManagedFields, appManagedFieldsCount)
    do {
        octopus = try OctopusSDK( apiKey: key, connectionMode: connMode)
        // Unity's Bundle.main doesn't include .lproj folders for all languages,
        // so the SDK's default language detection (Bundle.main.preferredLocalizations)
        // returns "en" regardless of the device language.
        // Override with the actual device locale so gRPC Accept-Language header is correct.
        octopus?.overrideDefaultLocale(with: Locale.current)
        notSeenNotifCancellable = octopus?.$notSeenNotificationsCount.sink { count in
            sendUnityMessage("OctopusChannel", "OnNotSeenNotificationsCount", String(count) )
        }

        // Both sdkInitialized and pendingNotificationResponse are read/written on the
        // main thread (from handleNotificationResponse). Dispatch here to avoid a data
        // race if OctopusSdkInitialize is ever called off-main.
        DispatchQueue.main.async {
            // Mark SDK as initialized so that future notification taps (warm/background)
            // send OnNotificationTapped directly from handleNotificationResponse.
            sdkInitialized = true

            // If a notification tap launched the app (cold start), the response was already
            // stored by OctopusAppController before Unity booted. Notify C# so the developer
            // can call Open().
            if OctopusNotificationState.shared.pendingNotificationResponse != nil {
                sendUnityMessage("OctopusChannel", "OnNotificationTapped", "")
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
public func OctopusSdkOpen(postId: UnsafePointer<Int8>) {
    // Copy the C string on the calling thread (the pointer is only valid here).
    var postIdString: String? = String(cString: postId)
    if postIdString?.isEmpty == true {
        postIdString = nil
    }

    // All UI state and @Published reads must happen on the main thread.
    DispatchQueue.main.async {
        // Force re-init when a notification response is pending or a specific post was requested,
        // so OctopusHomeScreen is re-created with the correct parameters.
        let hasPendingNotification = OctopusNotificationState.shared.pendingNotificationResponse != nil
        if hasPendingNotification || postIdString != nil {
            octopusController?.dismiss(animated: false)
            octopusController = nil
        }

        guard let sdk = octopus else {
            print("OctopusSdkOpen: SDK not initialized. Call OctopusSdkInitialize first.")
            return
        }
        guard let presentingVC = topViewController() else { return }

        if octopusController == nil {
            let navBarTitle: OctopusMainFeedTitle?
            if logo != nil {
                navBarTitle = OctopusMainFeedTitle(content: .logo, placement: .leading)
            } else if let name = appName, !name.isEmpty {
                navBarTitle = OctopusMainFeedTitle(
                    content: .text(.init(text: name)), placement: .leading)
            } else {
                navBarTitle = nil
            }

            let themeFonts = fonts ?? OctopusTheme.Fonts()
            let effectiveColorScheme = resolveColorScheme()

            // When a notification response is pending, it handles all navigation —
            // passing postId simultaneously would create a conflicting PostDetailView root.
            let effectivePostId = hasPendingNotification ? nil : postIdString

            let root = OctopusBridgeRootView(
                octopus: sdk,
                navBarTitle: navBarTitle,
                coloredNavBar: navBarUsesPrimaryColor,
                postId: effectivePostId,
                theme: OctopusTheme(
                    colors: effectiveColorScheme,
                    fonts: themeFonts,
                    assets: .init(logo: logo)
                )
            )
            octopusController = UIHostingController(rootView: AnyView(root))
            octopusController?.modalPresentationStyle = .fullScreen
        }

        if octopusController?.presentingViewController == nil {
            presentingVC.present(octopusController!, animated: true)
        }
    }
}

@_cdecl("OctopusSdkClose")
public func OctopusSdkClose(keepState: Bool = true) {
    DispatchQueue.main.async {
        octopusController?.dismiss(animated: true)
        if !keepState {
            octopusController = nil
            OctopusNotificationState.shared.pendingNotificationResponse = nil
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
        octopus?.connectUser(
            ClientUser(
                userId: userIdStr,
                profile: ClientUser.Profile(
                    nickname: nicknameStr,
                    bio: bioStr,
                    picture: pictureData
                )
            ),
            tokenProvider: {
                sendUnityMessage("OctopusChannel", "OnTokenRequested", "")
                return await withCheckedContinuation { continuation in
                    tokenCheckedContinuation = continuation
                }
            }
        )
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
