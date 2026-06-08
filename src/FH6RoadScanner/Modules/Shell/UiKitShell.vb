Public Enum UiKitDemoPage
    Scan = 0
    Settings = 1
    About = 2
End Enum

Public Class UiKitShellHost
    Public Property CurrentPage As UiKitDemoPage = UiKitDemoPage.Scan
    Public Property LastPage As UiKitDemoPage = UiKitDemoPage.Scan
End Class

Public Module UiKitShellText
    Public Function GetPageTitle(page As UiKitDemoPage) As String
        Select Case page
            Case UiKitDemoPage.Settings
                Return "设置"
            Case UiKitDemoPage.About
                Return "关于"
            Case Else
                Return "扫描"
        End Select
    End Function
End Module

Public Module UiKitShellNavigation
    Public Function GetActiveScroll(child As Object) As MyScrollViewer
        If child Is Nothing OrElse TypeOf child IsNot MyPageRight Then Return Nothing
        Dim page As MyPageRight = child
        If String.IsNullOrWhiteSpace(page.PanScroll) Then Return Nothing
        Return TryCast(page.FindName(page.PanScroll), MyScrollViewer)
    End Function
End Module
