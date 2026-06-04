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

private var octopusController: UIHostingController<AnyView>?
var notSeenNotifCancellable: AnyCancellable?
var groupsCancellable: AnyCancellable?

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
        groupsCancellable = octopus?.$groups.sink { groups in
            sendUnityMessage("OctopusChannel", "OnGroupsChanged", groupsToJson(groups))
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

@_cdecl("OctopusSdkOpenCreatePost")
public func OctopusSdkOpenCreatePost(
    text: UnsafePointer<Int8>, topicId: UnsafePointer<Int8>, imagePath: UnsafePointer<Int8>
) {
    let textStr = String(cString: text)
    let topicStr = String(cString: topicId)
    let pathStr = String(cString: imagePath)
    Task {
        let imageData = pathStr.isEmpty ? nil : await fetchImageData(fromPathOrUrl: pathStr)
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
                    cta: nil
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

private func presentHome(initialScreen: OctopusInitialScreen, payloadJson: String?) {
    // Decode the octopus payload (if any) into the userInfo["data"] shape the SDK expects.
    var userInfo: [AnyHashable: Any]? = nil
    if let payloadJson,
       let data = payloadJson.data(using: .utf8),
       let obj = try? JSONSerialization.jsonObject(with: data) as? [String: Any] {
        userInfo = ["data": obj]
    }

    DispatchQueue.main.async {
        // Re-create the controller whenever we have a specific destination so it
        // is built with the right initialScreen / notificationUserInfo.
        if userInfo != nil || !isMainFeed(initialScreen) {
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
            octopusController = UIHostingController(rootView: AnyView(root))
            octopusController?.modalPresentationStyle = .fullScreen
        }

        if octopusController?.presentingViewController == nil {
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
                    sendUnityMessage("OctopusChannel", "OnTokenRequested", "")
                    return await withCheckedContinuation { continuation in
                        tokenCheckedContinuation = continuation
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
