Imports System.Windows.Interop

Public Class FormMain

    Private ReadOnly PageHost As New UiKitShellHost()
    Private ReadOnly PageScan As PageUiKitRight
    Private ReadOnly PageSettings As PageUiKitRight
    Private ReadOnly PageAbout As PageUiKitRight
    Private ReadOnly StatusOverlay As StatusOverlayWindow
    Private IsSizeSaveable As Boolean
    Private IsClosingConfirmed As Boolean
    Private IsShutdownInProgress As Boolean

    Public ReadOnly Property Controller As ScannerAppController
    Public PageRight As MyPageRight
    Public Property Hidden As Boolean

    Public Sub New()
        ApplicationStartTick = GetTimeMs()
        FrmMain = Me
        ThemeCheckAll(False)
        ThemeRefresh(Settings.Get(Of Integer)("UiLauncherTheme"))

        Controller = New ScannerAppController()
        StatusOverlay = New StatusOverlayWindow(Controller)
        PageScan = PageUiKitRight.Create(UiKitDemoPage.Scan)
        PageSettings = PageUiKitRight.Create(UiKitDemoPage.Settings)
        PageAbout = PageUiKitRight.Create(UiKitDemoPage.About)

        InitializeComponent()
        Opacity = 0

        PanMainRight.Child = PageScan
        PageRight = PageScan
        PageHost.CurrentPage = UiKitDemoPage.Scan
        PageScan.PageState = MyPageRight.PageStates.ContentStay
    End Sub

    Private Async Sub FormMain_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        Handle = New WindowInteropHelper(Me).Handle
        UpdateBackgroundAndTitleBar()
        BtnExtraBack.ShowCheck = AddressOf BtnExtraBack_ShowCheck

        Dim resizer As New MyResizer(Me)
        resizer.addResizerDown(ResizerB)
        resizer.addResizerLeft(ResizerL)
        resizer.addResizerLeftDown(ResizerLB)
        resizer.addResizerLeftUp(ResizerLT)
        resizer.addResizerRight(ResizerR)
        resizer.addResizerRightDown(ResizerRB)
        resizer.addResizerRightUp(ResizerRT)
        resizer.addResizerUp(ResizerT)

        ThemeRefreshMain()
        BtnTitleSelect0.SetChecked(True, False, False)
        Height = Math.Max(Settings.Get(Of Integer)("WindowHeight"), MinHeight)
        Width = Math.Max(Settings.Get(Of Integer)("WindowWidth"), MinWidth)
        Top = (GetWPFSize(System.Windows.Forms.Screen.PrimaryScreen.WorkingArea.Height) - Height) / 2
        Left = (GetWPFSize(System.Windows.Forms.Screen.PrimaryScreen.WorkingArea.Width) - Width) / 2
        IsSizeSaveable = True
        ShowWindowToTop()
        StatusOverlay.ShowAtStartup()

        AniStart({
            AaCode(Sub() AniControlEnabled = 0, 50),
            AaOpacity(Me, Settings.Get(Of Integer)("UiLauncherTransparent") / 1000 + 0.4, 250, 100),
            AaDouble(Sub(i) TransformPos.Y += i, -TransformPos.Y, 600, 100, New AniEaseOutBack(AniEasePower.Weak)),
            AaDouble(Sub(i) TransformRotate.Angle += i, -TransformRotate.Angle, 500, 100, New AniEaseOutBack(AniEasePower.Weak)),
            AaCode(Sub()
                       PanBack.RenderTransform = Nothing
                   End Sub, , True)
        }, "Form Show")

        Try
            Await Controller.InitializeAsync()
            PageSettings.LoadConfiguration(Controller.Configuration)
            PageScan.SetBackendAvailability(True)
            Hint("扫描后端已连接。", HintType.Green)
        Catch ex As Exception
            Controller.AppendLocalLog("后端启动失败：" & ex.Message)
            Hint("扫描后端启动失败，请查看运行日志。", HintType.Red)
        End Try
    End Sub

    Public Shared Sub UpdateBackgroundAndTitleBar(Optional value As Object = Nothing)
        If FrmMain Is Nothing OrElse Not FrmMain.IsLoaded Then Return
        FrmMain.UpdateBackgroundAndTitleBar()
    End Sub

    Public Sub UpdateBackgroundAndTitleBar()
        ShapeTitleLogo.Visibility = Visibility.Collapsed
        LabTitleLogo.Visibility = Visibility.Visible
        LabTitleStatus.Visibility = Visibility.Visible
        ImageTitleLogo.Visibility = Visibility.Collapsed
        PanTitleSelect.Visibility = Visibility.Visible
        LabTitleLogo.Text = "FH6 ROAD SCANNER"
        LabTitleStatus.Text = "地平线道路扫描器"
        PanTitleMain.ColumnDefinitions(0).Width = New GridLength(1, GridUnitType.Star)
    End Sub

    Private Async Sub BtnTitleClose_Click(sender As Object, e As EventArgs) Handles BtnTitleClose.Click
        If IsClosingConfirmed Then
            Application.Current.Shutdown()
            Return
        End If
        If IsShutdownInProgress Then Return
        IsShutdownInProgress = True
        Try
            Await Controller.ShutdownAsync()
        Finally
            StatusOverlay.Detach()
            IsClosingConfirmed = True
            Application.Current.Shutdown()
        End Try
    End Sub

    Private Sub BtnTitleMin_Click(sender As Object, e As EventArgs) Handles BtnTitleMin.Click
        WindowState = WindowState.Minimized
    End Sub

    Private Sub FormDragMove(sender As Object, e As MouseButtonEventArgs) Handles PanTitle.MouseLeftButtonDown, PanMsg.MouseLeftButtonDown
        If e.ClickCount >= 2 Then
            WindowState = If(WindowState = WindowState.Maximized, WindowState.Normal, WindowState.Maximized)
        ElseIf sender.IsMouseDirectlyOver Then
            DragMove()
        End If
    End Sub

    Private Sub FormMain_SizeChanged() Handles Me.SizeChanged, Me.Loaded
        If IsSizeSaveable Then
            Settings.Set("WindowHeight", CInt(Height))
            Settings.Set("WindowWidth", CInt(Width))
        End If
        RectForm.Rect = New Rect(0, 0, BorderForm.ActualWidth, BorderForm.ActualHeight)
        PanForm.Width = BorderForm.ActualWidth + 0.001
        PanForm.Height = BorderForm.ActualHeight + 0.001
        PanMain.Width = PanForm.Width
        PanMain.Height = Math.Max(0, PanForm.Height - PanTitle.ActualHeight)
        If WindowState = WindowState.Maximized Then WindowState = WindowState.Normal
    End Sub

    Private Sub FormMain_Closing(sender As Object, e As ComponentModel.CancelEventArgs) Handles Me.Closing
        If IsClosingConfirmed Then Return
        e.Cancel = True
        BtnTitleClose_Click(Me, EventArgs.Empty)
    End Sub

    Private Sub BtnTitleSelect_Click(sender As MyRadioButton, raiseByMouse As Boolean) Handles BtnTitleSelect0.Check, BtnTitleSelect1.Check, BtnTitleSelect2.Check
        PageChange(CType(Val(sender.Tag), UiKitDemoPage))
    End Sub

    Public Sub PageChange(page As UiKitDemoPage)
        If PageHost.CurrentPage = page Then Return
        PageHost.LastPage = PageHost.CurrentPage
        PageHost.CurrentPage = page

        Dim target = GetRightPage(page)
        PageChangeAnim(target)
        Hint("已切换到：" & UiKitShellText.GetPageTitle(page))
    End Sub

    Private Function GetRightPage(page As UiKitDemoPage) As PageUiKitRight
        Select Case page
            Case UiKitDemoPage.Settings
                Return PageSettings
            Case UiKitDemoPage.About
                Return PageAbout
            Case Else
                Return PageScan
        End Select
    End Function

    Public Function TryGetCurrentConfiguration(ByRef config As ScannerConfiguration, Optional saveToDisk As Boolean = False) As Boolean
        Try
            config = PageSettings.ReadConfiguration()
            Controller.Configuration = config
            If saveToDisk OrElse config.AutoSave Then Controller.SaveConfiguration()
            Return True
        Catch ex As Exception
            MyMsgBox(ex.Message, "参数错误", IsWarn:=True)
            PageChange(UiKitDemoPage.Settings)
            Return False
        End Try
    End Function

    Private Sub PageChangeAnim(target As MyPageRight)
        If target Is Nothing Then Return
        AniStop("FrmMain PageChangeRight")
        AniControlEnabled += 1
        If TypeOf PanMainRight.Child Is MyPageRight Then CType(PanMainRight.Child, MyPageRight).PageOnExit()
        AniControlEnabled -= 1
        AniStart({
            AaCode(Sub()
                       AniControlEnabled += 1
                       If TypeOf PanMainRight.Child Is MyPageRight Then CType(PanMainRight.Child, MyPageRight).PageOnForceExit()
                       PanMainRight.Child = target
                       target.Opacity = 0
                       AniControlEnabled -= 1
                       BtnExtraBack.ShowRefresh()
                   End Sub, 110),
            AaCode(Sub()
                       target.Opacity = 1
                       target.PageOnEnter()
                   End Sub, 30, True)
        }, "FrmMain PageChangeRight")
    End Sub

    Public Sub ShowWindowToTop()
        Visibility = Visibility.Visible
        ShowInTaskbar = True
        WindowState = WindowState.Normal
        Topmost = True
        Topmost = False
        Activate()
        Focus()
    End Sub

    Public Sub BackToTop() Handles BtnExtraBack.Click
        Dim scroll = UiKitShellNavigation.GetActiveScroll(PanMainRight.Child)
        If scroll IsNot Nothing Then scroll.PerformVerticalOffsetDelta(-scroll.VerticalOffset)
    End Sub

    Private Function BtnExtraBack_ShowCheck() As Boolean
        Dim scroll = UiKitShellNavigation.GetActiveScroll(PanMainRight.Child)
        Return scroll IsNot Nothing AndAlso scroll.Visibility = Visibility.Visible AndAlso scroll.VerticalOffset > Height + If(BtnExtraBack.Show, 0, 700)
    End Function

    Public Sub DragDoing()
    End Sub

    Public Sub DragStop()
    End Sub

    Public Sub DragTick()
    End Sub

    Public Sub SliderDrag_Finish()
    End Sub

    Public Shared Sub EndProgramForce(returnValue As ProcessReturnValues)
        Environment.Exit(CInt(returnValue))
    End Sub

End Class
