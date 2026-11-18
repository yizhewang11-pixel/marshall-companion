Marshall Companion 6.7 — Windows x64

开始使用 / Getting started
从托盘退出旧版，完整解压，双击 MarshallCompanion.exe。
Exit the old app from its tray menu, extract the entire ZIP, and run MarshallCompanion.exe.
需要 Windows 10/11 x64、.NET Framework 4.8 和 Microsoft Edge WebView2 Runtime。
Requires Windows 10/11 x64, .NET Framework 4.8 and Microsoft Edge WebView2 Runtime.
若缺少 WebView2 Runtime，请从微软安装：https://developer.microsoft.com/microsoft-edge/webview2/
If WebView2 Runtime is missing, install it from Microsoft at the URL above.

设备选择 / Device selection
先在 Windows 配对音箱，点击下排中间“检测与设置”，刷新列表，选择目标并保存。
Pair your speaker in Windows. Open Checks & setup (bottom center), refresh, choose a target and save.
本版本移除了型号和代数白名单：通过 Windows 蓝牙音频设备信息识别，而非依靠 Acton 或 Marshall 名称，所以改名后的设备也可选择。
Model and generation allowlists have been removed. Detection uses Windows Bluetooth audio device information rather than Acton or Marshall names, so renamed devices can be selected.
列表可能包含其他品牌的蓝牙音频输出，请自行确认目标。免提通话输出会按名称排除。
Other brands may appear in the list. Confirm the target yourself. Hands-free outputs are excluded by name.
未选择目标时不自动控制其他设备。旧版已保存的目标可继续使用；若没有保存，请重新选择。设备重新配对或驱动更新后可能需要重新选择。
No target means no automatic control of another device. Existing saved targets are retained; select one if none was saved. Re-pairing or driver changes may require reselection.
只有 Windows 已建立、且程序能确认其蓝牙来源的音频输出会列出。仅支持 Wi-Fi、没有蓝牙音频输出或驱动无法识别的设备不在支持范围。
Only Windows audio outputs whose Bluetooth origin can be confirmed are listed. Wi-Fi-only devices, devices without a Bluetooth audio output and unrecognized drivers are not supported.
“取消型号限制”不代表全系列全代均经过硬件验证。重连依赖 Windows 驱动，保活效果也取决于音箱固件的待机行为。
Removing model filters does not mean every model and generation has been hardware-verified. Reconnect depends on the Windows driver; keep-awake effectiveness also depends on speaker firmware.

自检与测试 / Checks and tests
环境自检显示运行时、可识别设备和目标情况，不把环境就绪当作驱动兼容证明。
Environment checks show runtime and detected devices, not proof of driver compatibility.
重连测试：目标断开且不在免打扰时向驱动发送请求，最多观察 20 秒。已连接时不会主动断开。超时表示未确认，不等于确定不支持。
Reconnect test: requests connection when disconnected and outside quiet hours, then observes for up to 20 seconds. It never disconnects an existing connection. Timeout means unconfirmed, not definitively unsupported.
保活测试（会发声）：向选定且已连接的设备播放 2 秒轻音。按端点 ID 精确匹配播放通道，不使用电脑默认输出，也不按同名猜测；无法映射则停止并显示错误。
Test tone (audible): plays a 2-second quiet tone to the selected connected output. The playback channel is matched by endpoint ID, never by name or the computer default. Mapping failure stops playback and reports an error.
测试音成功表示播放调用完成；音箱是否实际发声及长时间防止待机仍需观察。
Completed tone playback means the playback operation finished; audible output and long-term standby prevention still require observation.

日常功能 / Daily operation
自动重连间隔 1–60 分钟；连接后切换系统音频输出，未完成连接不会伪装成功。
Reconnect interval: 1–60 minutes. System audio switches after connection; a pending request is not displayed as success.
按需保活：监测所选输出，连续约 8 分钟无声后播放 2 秒 220Hz、约 -50dBFS 轻音。实际是否听得到取决于音箱音量。音量检测不可用时暂停保活。
Keep awake: meters the selected output; after about 8 minutes of silence, plays a 2-second 220Hz tone at about -50dBFS. Audibility depends on speaker volume. Unavailable metering pauses automatic tones.
夜间免打扰开关和时间合并在下排左侧。默认每日 00:00–08:00，按电脑当地时间。期间暂停重连、输出切换和测试/保活音；不主动中断已有播放。起止相同表示全天。
Quiet-hours enable and times are combined at bottom left. Default: daily 00:00–08:00 in local computer time. Reconnect, output switching and tones pause; existing playback is not disconnected. Equal start/end means all day.
默认 80% 大小，大字号；拖动边缘或四角可等比例缩放。受屏幕可用区域限制时自动缩小。
Default 80% size with larger text. Drag edges/corners for proportional resizing. A small screen work area may reduce the scale.
点 × 或最小化后留在托盘；右键托盘 > 退出 才停止。
Close/minimize keeps the app in the tray. Right-click tray > Exit to stop.
程序无法打开断电音箱，也不能在电脑睡眠或关机期间运行。
The app cannot power on an unpowered speaker or run while the PC is asleep or off.

开机自启动 / Login startup
退出程序后运行 Enable-Autostart.cmd，它会把程序复制到固定本机目录。每次更新后须从新文件夹重新运行此脚本。
Exit the app and run Enable-Autostart.cmd to copy it to a stable local folder. Run it again from the new folder after each update.
Disable-Autostart.cmd 关闭自启动，不删除设置。
Disable-Autostart.cmd disables login startup without deleting settings.
设置位于 %LOCALAPPDATA%\MarshallCompanion\settings.json，日志保存本次运行最近 80 条。
Settings: %LOCALAPPDATA%\MarshallCompanion\settings.json. The log retains the latest 80 events of the current session.

验证范围 / Validation
已通过 x64 编译、JavaScript 语法和模拟界面消息测试。未在各型号音箱及实际 Windows 蓝牙硬件上测试。
Passed x64 compilation, JavaScript syntax and mocked UI message tests. Not hardware-tested across speaker models or Windows Bluetooth adapters.

6.6：点亮红灯增加中心高光及轻微光晕；已连接拨杆不再整体灰化。
6.6: Lit LEDs gain a bright core and subtle halo; connected controls retain full opacity.

6.7：检测与设置页面内可开启/关闭开机自启动，实际创建或删除当前用户的启动快捷方式；开启时安装到固定本机目录，下次登录在托盘运行。无需管理员权限。
6.7: Toggle Start with Windows in Checks & setup. Creates/removes the current-user startup shortcut; enabling installs a stable local copy and starts in the tray at next sign-in. No administrator rights required.
