Imports System.Globalization
Imports System.Text.Json

Public Class ScannerConfiguration
    Public Property ScreenW As Integer
    Public Property ScreenH As Integer
    Public Property ScanX As Integer
    Public Property ScanY As Integer
    Public Property ScanW As Integer
    Public Property ScanH As Integer
    Public Property DetectX As Integer
    Public Property DetectY As Integer
    Public Property DetectW As Integer
    Public Property DetectH As Integer
    Public Property StepX As Integer = 20
    Public Property StepY As Integer = 20
    Public Property MoveDelay As Double = 0.006
    Public Property DiffThreshold As Double = 14
    Public Property StartDelay As Double = 5
    Public Property StopOnHit As Boolean = True
    Public Property AutoSave As Boolean = True
    Public Property StartHotkey As String = "F7"
    Public Property StopHotkey As String = "F8"

    Public ReadOnly Property ScanRegion As Integer()
        Get
            Return {ScanX, ScanY, ScanW, ScanH}
        End Get
    End Property

    Public ReadOnly Property DetectRegion As Integer()
        Get
            Return {DetectX, DetectY, DetectW, DetectH}
        End Get
    End Property

    Public Shared Function CreateDefault(screenW As Integer, screenH As Integer) As ScannerConfiguration
        Return New ScannerConfiguration With {
            .ScreenW = screenW,
            .ScreenH = screenH,
            .ScanX = CInt(screenW * 0.01),
            .ScanY = CInt(screenH * 0.01),
            .ScanW = CInt(screenW * 0.98),
            .ScanH = CInt(screenH * 0.84),
            .DetectX = CInt(screenW * 0.215),
            .DetectY = CInt(screenH * 0.895),
            .DetectW = CInt(screenW * 0.125),
            .DetectH = CInt(screenH * 0.065)
        }
    End Function
End Class

Public Class ScannerConfigService
    Private ReadOnly ConfigPath As String
    Private ReadOnly ScreenW As Integer
    Private ReadOnly ScreenH As Integer

    Public Sub New(baseDirectory As String, screenW As Integer, screenH As Integer)
        ConfigPath = IO.Path.Combine(baseDirectory, "config.json")
        Me.ScreenW = screenW
        Me.ScreenH = screenH
    End Sub

    Public Function Load() As ScannerConfiguration
        Dim defaults = ScannerConfiguration.CreateDefault(ScreenW, ScreenH)
        If Not IO.File.Exists(ConfigPath) Then Return defaults

        Try
            Using document = JsonDocument.Parse(IO.File.ReadAllText(ConfigPath, Text.Encoding.UTF8))
                Dim root = document.RootElement
                Dim savedW = ReadInt(root, "screen_w", 0)
                Dim savedH = ReadInt(root, "screen_h", 0)
                Dim keepRegions = savedW = 0 OrElse savedH = 0 OrElse (savedW = ScreenW AndAlso savedH = ScreenH)

                defaults.ScreenW = ScreenW
                defaults.ScreenH = ScreenH
                If keepRegions Then
                    defaults.ScanX = ReadInt(root, "scan_x", defaults.ScanX)
                    defaults.ScanY = ReadInt(root, "scan_y", defaults.ScanY)
                    defaults.ScanW = ReadInt(root, "scan_w", defaults.ScanW)
                    defaults.ScanH = ReadInt(root, "scan_h", defaults.ScanH)
                    defaults.DetectX = ReadInt(root, "detect_x", defaults.DetectX)
                    defaults.DetectY = ReadInt(root, "detect_y", defaults.DetectY)
                    defaults.DetectW = ReadInt(root, "detect_w", defaults.DetectW)
                    defaults.DetectH = ReadInt(root, "detect_h", defaults.DetectH)
                End If

                defaults.StepX = ReadInt(root, "step_x", defaults.StepX)
                defaults.StepY = ReadInt(root, "step_y", defaults.StepY)
                defaults.MoveDelay = ReadDouble(root, "move_delay", defaults.MoveDelay)
                defaults.DiffThreshold = ReadDouble(root, "diff_threshold", defaults.DiffThreshold)
                defaults.StartDelay = ReadDouble(root, "start_delay", defaults.StartDelay)
                defaults.StopOnHit = ReadBoolean(root, "stop_on_hit", defaults.StopOnHit)
                defaults.AutoSave = ReadBoolean(root, "auto_save", defaults.AutoSave)
                defaults.StartHotkey = ReadString(root, "start_hotkey", defaults.StartHotkey)
                defaults.StopHotkey = ReadString(root, "stop_hotkey", defaults.StopHotkey)
                If Not GlobalHotkeyService.IsSupported(defaults.StartHotkey) Then defaults.StartHotkey = "F7"
                If Not GlobalHotkeyService.IsSupported(defaults.StopHotkey) Then defaults.StopHotkey = "F8"
                If defaults.StartHotkey = defaults.StopHotkey Then
                    defaults.StartHotkey = "F7"
                    defaults.StopHotkey = "F8"
                End If
            End Using
        Catch ex As Exception
            Logger.Warn(ex, "读取扫描配置失败，使用默认值")
        End Try
        Return defaults
    End Function

    Public Sub Save(config As ScannerConfiguration)
        Dim data As New Dictionary(Of String, Object) From {
            {"screen_w", config.ScreenW},
            {"screen_h", config.ScreenH},
            {"scan_x", config.ScanX},
            {"scan_y", config.ScanY},
            {"scan_w", config.ScanW},
            {"scan_h", config.ScanH},
            {"detect_x", config.DetectX},
            {"detect_y", config.DetectY},
            {"detect_w", config.DetectW},
            {"detect_h", config.DetectH},
            {"step_x", config.StepX},
            {"step_y", config.StepY},
            {"move_delay", config.MoveDelay},
            {"diff_threshold", config.DiffThreshold},
            {"start_delay", config.StartDelay},
            {"stop_on_hit", config.StopOnHit},
            {"auto_save", config.AutoSave},
            {"start_hotkey", config.StartHotkey},
            {"stop_hotkey", config.StopHotkey}
        }
        Dim options As New JsonSerializerOptions With {.WriteIndented = True}
        IO.File.WriteAllText(ConfigPath, JsonSerializer.Serialize(data, options), New Text.UTF8Encoding(False))
    End Sub

    Private Shared Function ReadInt(root As JsonElement, name As String, fallback As Integer) As Integer
        Dim value As JsonElement
        If Not root.TryGetProperty(name, value) Then Return fallback
        Dim result As Integer
        If value.ValueKind = JsonValueKind.Number AndAlso value.TryGetInt32(result) Then Return result
        If Integer.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, result) Then Return result
        Return fallback
    End Function

    Private Shared Function ReadDouble(root As JsonElement, name As String, fallback As Double) As Double
        Dim value As JsonElement
        If Not root.TryGetProperty(name, value) Then Return fallback
        Dim result As Double
        If value.ValueKind = JsonValueKind.Number AndAlso value.TryGetDouble(result) Then Return result
        If Double.TryParse(value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, result) Then Return result
        Return fallback
    End Function

    Private Shared Function ReadBoolean(root As JsonElement, name As String, fallback As Boolean) As Boolean
        Dim value As JsonElement
        If Not root.TryGetProperty(name, value) Then Return fallback
        If value.ValueKind = JsonValueKind.True Then Return True
        If value.ValueKind = JsonValueKind.False Then Return False
        Dim result As Boolean
        If Boolean.TryParse(value.ToString(), result) Then Return result
        Return fallback
    End Function

    Private Shared Function ReadString(root As JsonElement, name As String, fallback As String) As String
        Dim value As JsonElement
        If Not root.TryGetProperty(name, value) OrElse value.ValueKind <> JsonValueKind.String Then Return fallback
        Dim result = value.GetString()
        If String.IsNullOrWhiteSpace(result) Then Return fallback
        Return result.Trim().ToUpperInvariant()
    End Function
End Class
