import AppKit
import CoreGraphics
import ScreenCaptureKit
import Vision

let lifetime = DispatchSource.makeTimerSource(queue: .global(qos: .utility))
if let parent = ProcessInfo.processInfo.environment["CAMPUS_WINDOW_PARENT"].flatMap(Int32.init) {
    lifetime.schedule(deadline: .now(), repeating: .milliseconds(200))
    lifetime.setEventHandler { if getppid() != parent { _exit(0) } }
    lifetime.activate()
}

struct StudyWindow: Codable, Equatable {
    let id: String
    let pid: Int32
    let app: String
    let name: String
    let title: String
}

func windows() -> [StudyWindow] {
    let options: CGWindowListOption = [.optionOnScreenOnly, .excludeDesktopElements]
    guard let items = CGWindowListCopyWindowInfo(options, kCGNullWindowID) as? [[String: Any]] else { return [] }
    return items.compactMap { item in
        guard item[kCGWindowLayer as String] as? Int == 0,
              let id = item[kCGWindowNumber as String] as? UInt32,
              let pid = item[kCGWindowOwnerPID as String] as? Int32,
              let bounds = item[kCGWindowBounds as String] as? [String: CGFloat],
              (bounds["Width"] ?? 0) >= 100, (bounds["Height"] ?? 0) >= 80,
              let app = NSRunningApplication(processIdentifier: pid),
              app.activationPolicy == .regular, let identifier = app.bundleIdentifier else { return nil }
        return StudyWindow(id: String(id), pid: pid, app: identifier,
                           name: app.localizedName ?? identifier,
                           title: String((item[kCGWindowName as String] as? String ?? "").prefix(300)))
    }
}

func activeWindow() -> StudyWindow? {
    guard let pid = NSWorkspace.shared.frontmostApplication?.processIdentifier else { return nil }
    return windows().first { $0.pid == pid }
}

func dictionary(_ window: StudyWindow) throws -> Any {
    try JSONSerialization.jsonObject(with: JSONEncoder().encode(window))
}

func output(_ value: [String: Any]) throws {
    let data = try JSONSerialization.data(withJSONObject: value)
    FileHandle.standardOutput.write(data)
}

func capture(_ selected: StudyWindow) async throws {
    let content = try await SCShareableContent.excludingDesktopWindows(true, onScreenWindowsOnly: true)
    guard let window = content.windows.first(where: {
        String($0.windowID) == selected.id && $0.owningApplication?.processID == selected.pid
    }), activeWindow() == selected else {
        try output([:])
        return
    }
    let filter = SCContentFilter(desktopIndependentWindow: window)
    let config = SCStreamConfiguration()
    let scale = min(1, 1280 / max(window.frame.width, window.frame.height))
    config.width = max(1, Int(window.frame.width * scale))
    config.height = max(1, Int(window.frame.height * scale))
    config.showsCursor = false
    let image = try await SCScreenshotManager.captureImage(contentFilter: filter, configuration: config)
    guard activeWindow() == selected else {
        try output([:])
        return
    }
    let recognition = VNRecognizeTextRequest()
    recognition.recognitionLevel = .accurate
    recognition.recognitionLanguages = ["ko-KR", "en-US"]
    try? VNImageRequestHandler(cgImage: image).perform([recognition])
    let words = (recognition.results ?? []).compactMap { $0.topCandidates(1).first?.string }.joined(separator: " ")
    let jpeg = NSBitmapImageRep(cgImage: image).representation(
        using: .jpeg, properties: [.compressionFactor: 0.85])
    try output(["image": jpeg?.base64EncodedString() ?? "", "words": String(words.prefix(4000))])
}

do {
    let arguments = CommandLine.arguments
    switch arguments.dropFirst().first {
    case "permission":
        try output(["permitted": CGPreflightScreenCaptureAccess()])
    case "request":
        try output(["permitted": CGRequestScreenCaptureAccess()])
    case "list":
        try output(["windows": try CGPreflightScreenCaptureAccess() ? windows().prefix(50).map(dictionary) : []])
    case "active":
        let foreground = CGPreflightScreenCaptureAccess() ? activeWindow() : nil
        try output(["window": try foreground.map(dictionary) ?? NSNull()])
    case "capture":
        guard arguments.count == 4, CGPreflightScreenCaptureAccess(),
              let window = activeWindow(), window.id == arguments[2], String(window.pid) == arguments[3] else {
            try output([:])
            break
        }
        try await capture(window)
    default:
        try output([:])
    }
} catch {
    // Do not persist images, text, or window titles in diagnostic logs.
    try? output([:])
}
