namespace MidiToEverything.App.Localization;

/// <summary>
/// Localized strings (docs/07). Scope of this pass: the main window, window chrome, and the tray —
/// the always-visible surface and the language switch itself. The profile editor remains Japanese
/// (internally consistent) and is the next localization increment.
/// </summary>
internal static class Strings
{
    public static readonly IReadOnlyDictionary<string, string> Ja = new Dictionary<string, string>
    {
        ["common.emergencyToggle"] = "緊急停止 切替",

        ["main.profileLabel"] = "プロファイル: ",
        ["main.pinned"] = "固定",
        ["main.status.running"] = "稼働中",
        ["main.status.stopped"] = "停止中 (緊急停止)",
        ["main.allowLaunch"] = "外部起動を許可",
        ["main.allowLaunch.tip"] = "起動アクション（アプリ/ファイル/URL）を有効化。安全のため既定は無効",
        ["main.editProfiles"] = "プロファイル編集",
        ["main.devices"] = "デバイス",
        ["main.detect.auto"] = "自動更新",
        ["main.detect.manual"] = "手動更新",
        ["main.detect.tip"] = "自動更新: 定期的にデバイスを検出 ／ 手動更新: 「今すぐ更新」を押した時だけ検出",
        ["main.refreshNow"] = "今すぐ更新",
        ["main.notConnected"] = "(未接続)",
        ["main.obs.title"] = "OBS接続 (obs-websocket)",
        ["main.obs.host.tip"] = "ホスト",
        ["main.obs.port.tip"] = "ポート (既定 4455)",
        ["main.obs.pass.tip"] = "OBSのWebSocket設定のパスワード（未設定なら空欄）",
        ["main.monitor"] = "入力モニター",
        ["main.pause"] = "一時停止",
        ["main.resume"] = "再開",
        ["main.clear"] = "クリア",
        ["main.language"] = "言語",

        ["tray.show"] = "表示",
        ["tray.startup"] = "Windows起動時に実行",
        ["tray.exit"] = "終了",

        ["lang.ja"] = "日本語",
        ["lang.en"] = "English",
    };

    public static readonly IReadOnlyDictionary<string, string> En = new Dictionary<string, string>
    {
        ["common.emergencyToggle"] = "Emergency stop",

        ["main.profileLabel"] = "Profile: ",
        ["main.pinned"] = "Pinned",
        ["main.status.running"] = "Running",
        ["main.status.stopped"] = "Stopped (emergency)",
        ["main.allowLaunch"] = "Allow external launch",
        ["main.allowLaunch.tip"] = "Enable launch actions (apps/files/URLs). Off by default for safety.",
        ["main.editProfiles"] = "Edit profiles",
        ["main.devices"] = "Devices",
        ["main.detect.auto"] = "Auto-detect",
        ["main.detect.manual"] = "Manual",
        ["main.detect.tip"] = "Auto: poll for devices periodically  /  Manual: detect only on \"Refresh now\"",
        ["main.refreshNow"] = "Refresh now",
        ["main.notConnected"] = "(none)",
        ["main.obs.title"] = "OBS connection (obs-websocket)",
        ["main.obs.host.tip"] = "Host",
        ["main.obs.port.tip"] = "Port (default 4455)",
        ["main.obs.pass.tip"] = "OBS WebSocket password (leave blank if not set)",
        ["main.monitor"] = "Input monitor",
        ["main.pause"] = "Pause",
        ["main.resume"] = "Resume",
        ["main.clear"] = "Clear",
        ["main.language"] = "Language",

        ["tray.show"] = "Show",
        ["tray.startup"] = "Run at Windows startup",
        ["tray.exit"] = "Exit",

        ["lang.ja"] = "日本語",
        ["lang.en"] = "English",
    };
}
