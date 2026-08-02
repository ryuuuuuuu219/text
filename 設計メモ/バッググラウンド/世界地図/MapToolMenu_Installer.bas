Attribute VB_Name = "MapToolMenuInstaller"
Option Explicit

Public Sub InstallMapToolMenu()
    Const vbext_ct_MSForm As Long = 3

    Dim vbProj As Object
    Dim vbComp As Object
    Dim designer As Object
    Dim fraResolution As Object
    Dim fraCoast As Object
    Dim fraLabel As Object
    Dim stepName As String

    On Error GoTo InstallError

    stepName = "Open VBA project"
    Set vbProj = ThisWorkbook.VBProject

    stepName = "Remove old form"
    RemoveComponentIfExists vbProj, "MapToolMenu"

    stepName = "Create UserForm"
    Set vbComp = vbProj.VBComponents.Add(vbext_ct_MSForm)
    vbComp.Name = "MapToolMenu"
    Set designer = vbComp.Designer

    stepName = "Set form properties"
    With designer
        .Caption = "Map Tools"
        .Width = 270
        .Height = 330
    End With

    stepName = "Create top controls"
    AddLabel designer, "lblSelection", "Selection: none", 12, 12, 240, 18
    AddButton designer, "btnRefresh", "Refresh Selection", 12, 34, 240, 24

    stepName = "Create resolution controls"
    AddButton designer, "btnResolutionHeader", "- Resolution Tools", 12, 66, 240, 24
    Set fraResolution = AddFrame(designer, "fraResolution", 12, 90, 240, 92)
    AddButton fraResolution, "btnDouble", "Double Rows and Columns", 10, 12, 216, 22
    AddButton fraResolution, "btnReduceRows", "Reduce Rows by Half", 10, 38, 216, 22
    AddButton fraResolution, "btnReduceColumns", "Reduce Columns by Half", 10, 64, 216, 22

    stepName = "Create coastline controls"
    AddButton designer, "btnCoastHeader", "+ Coastline Tools", 12, 190, 240, 24
    Set fraCoast = AddFrame(designer, "fraCoast", 12, 214, 240, 46)
    fraCoast.Visible = False
    AddButton fraCoast, "btnRoughen", "Roughen Selected Coastline", 10, 12, 216, 22

    stepName = "Create label controls"
    AddButton designer, "btnLabelHeader", "+ Label Tools", 12, 268, 240, 24
    Set fraLabel = AddFrame(designer, "fraLabel", 12, 292, 240, 46)
    fraLabel.Visible = False
    AddButton fraLabel, "btnTextBox", "Insert Text Box", 10, 12, 216, 22

    stepName = "Create close button"
    AddButton designer, "btnClose", "Close", 12, 346, 240, 24

    stepName = "Insert form code"
    vbComp.CodeModule.AddFromString GetFormCode()

    MsgBox "MapToolMenu was installed successfully.", vbInformation
    Exit Sub

InstallError:
    MsgBox "Installation failed." & vbCrLf & vbCrLf & _
           "Step: " & stepName & vbCrLf & _
           "Error " & Err.Number & ": " & Err.Description & vbCrLf & vbCrLf & _
           "Confirm that Trust access to the VBA project object model is enabled.", vbCritical
End Sub

Public Sub ShowMapTools()
    Dim frm As Object

    On Error GoTo ShowError
    Set frm = VBA.UserForms.Add("MapToolMenu")
    frm.Show vbModeless
    Exit Sub

ShowError:
    MsgBox "MapToolMenu is not installed. Run InstallMapToolMenu first." & vbCrLf & _
           "Error " & Err.Number & ": " & Err.Description, vbExclamation
End Sub

Private Sub RemoveComponentIfExists(ByVal vbProj As Object, ByVal componentName As String)
    Dim comp As Object

    For Each comp In vbProj.VBComponents
        If StrComp(comp.Name, componentName, vbTextCompare) = 0 Then
            vbProj.VBComponents.Remove comp
            Exit For
        End If
    Next comp
End Sub

Private Function AddButton(ByVal parent As Object, ByVal controlName As String, _
                           ByVal captionText As String, ByVal leftPos As Single, _
                           ByVal topPos As Single, ByVal controlWidth As Single, _
                           ByVal controlHeight As Single) As Object
    Dim ctl As Object

    Set ctl = parent.Controls.Add("Forms.CommandButton.1", controlName, True)
    With ctl
        .Caption = captionText
        .Left = leftPos
        .Top = topPos
        .Width = controlWidth
        .Height = controlHeight
    End With

    Set AddButton = ctl
End Function

Private Function AddLabel(ByVal parent As Object, ByVal controlName As String, _
                          ByVal captionText As String, ByVal leftPos As Single, _
                          ByVal topPos As Single, ByVal controlWidth As Single, _
                          ByVal controlHeight As Single) As Object
    Dim ctl As Object

    Set ctl = parent.Controls.Add("Forms.Label.1", controlName, True)
    With ctl
        .Caption = captionText
        .Left = leftPos
        .Top = topPos
        .Width = controlWidth
        .Height = controlHeight
    End With

    Set AddLabel = ctl
End Function

Private Function AddFrame(ByVal parent As Object, ByVal controlName As String, _
                          ByVal leftPos As Single, ByVal topPos As Single, _
                          ByVal controlWidth As Single, ByVal controlHeight As Single) As Object
    Dim ctl As Object

    Set ctl = parent.Controls.Add("Forms.Frame.1", controlName, True)
    With ctl
        .Caption = ""
        .Left = leftPos
        .Top = topPos
        .Width = controlWidth
        .Height = controlHeight
    End With

    Set AddFrame = ctl
End Function

Private Function GetFormCode() As String
    Dim s As String

    AppendLine s, "Option Explicit"
    AppendLine s, "Private resolutionExpanded As Boolean"
    AppendLine s, "Private coastExpanded As Boolean"
    AppendLine s, "Private labelExpanded As Boolean"
    AppendLine s, ""
    AppendLine s, "Private Sub UserForm_Initialize()"
    AppendLine s, "    resolutionExpanded = True"
    AppendLine s, "    coastExpanded = False"
    AppendLine s, "    labelExpanded = False"
    AppendLine s, "    RefreshSelectionText"
    AppendLine s, "    LayoutForm"
    AppendLine s, "End Sub"
    AppendLine s, ""
    AppendLine s, "Private Sub UserForm_Activate()"
    AppendLine s, "    RefreshSelectionText"
    AppendLine s, "End Sub"
    AppendLine s, ""
    AppendLine s, "Private Sub btnRefresh_Click()"
    AppendLine s, "    RefreshSelectionText"
    AppendLine s, "End Sub"
    AppendLine s, ""
    AppendLine s, "Private Sub btnResolutionHeader_Click()"
    AppendLine s, "    resolutionExpanded = Not resolutionExpanded"
    AppendLine s, "    LayoutForm"
    AppendLine s, "End Sub"
    AppendLine s, ""
    AppendLine s, "Private Sub btnCoastHeader_Click()"
    AppendLine s, "    coastExpanded = Not coastExpanded"
    AppendLine s, "    LayoutForm"
    AppendLine s, "End Sub"
    AppendLine s, ""
    AppendLine s, "Private Sub btnLabelHeader_Click()"
    AppendLine s, "    labelExpanded = Not labelExpanded"
    AppendLine s, "    LayoutForm"
    AppendLine s, "End Sub"
    AppendLine s, ""
    AppendLine s, "Private Sub btnDouble_Click()"
    AppendLine s, "    DoubleMapResolution"
    AppendLine s, "    RefreshSelectionText"
    AppendLine s, "End Sub"
    AppendLine s, ""
    AppendLine s, "Private Sub btnReduceRows_Click()"
    AppendLine s, "    ReduceRowResolution"
    AppendLine s, "    RefreshSelectionText"
    AppendLine s, "End Sub"
    AppendLine s, ""
    AppendLine s, "Private Sub btnReduceColumns_Click()"
    AppendLine s, "    ReduceColumnResolution"
    AppendLine s, "    RefreshSelectionText"
    AppendLine s, "End Sub"
    AppendLine s, ""
    AppendLine s, "Private Sub btnRoughen_Click()"
    AppendLine s, "    RoughenCoastline"
    AppendLine s, "    RefreshSelectionText"
    AppendLine s, "End Sub"
    AppendLine s, ""
    AppendLine s, "Private Sub btnTextBox_Click()"
    AppendLine s, "    InsertMapTextBox"
    AppendLine s, "    RefreshSelectionText"
    AppendLine s, "End Sub"
    AppendLine s, ""
    AppendLine s, "Private Sub btnClose_Click()"
    AppendLine s, "    Unload Me"
    AppendLine s, "End Sub"
    AppendLine s, ""
    AppendLine s, "Private Sub RefreshSelectionText()"
    AppendLine s, "    On Error GoTo NoSelection"
    AppendLine s, "    If TypeName(Selection) = ""Range"" Then"
    AppendLine s, "        lblSelection.Caption = ""Selection: "" & Selection.Worksheet.Name & ""!"" & Selection.Address(False, False) & ""  ("" & Selection.Rows.Count & "" x "" & Selection.Columns.Count & "")"""
    AppendLine s, "    Else"
    AppendLine s, "        lblSelection.Caption = ""Selection: none"""
    AppendLine s, "    End If"
    AppendLine s, "    Exit Sub"
    AppendLine s, "NoSelection:"
    AppendLine s, "    lblSelection.Caption = ""Selection: unavailable"""
    AppendLine s, "End Sub"
    AppendLine s, ""
    AppendLine s, "Private Sub LayoutForm()"
    AppendLine s, "    Dim y As Single"
    AppendLine s, "    y = 66"
    AppendLine s, "    btnResolutionHeader.Top = y"
    AppendLine s, "    btnResolutionHeader.Caption = IIf(resolutionExpanded, ""- Resolution Tools"", ""+ Resolution Tools"")"
    AppendLine s, "    y = y + 24"
    AppendLine s, "    fraResolution.Top = y"
    AppendLine s, "    fraResolution.Visible = resolutionExpanded"
    AppendLine s, "    If resolutionExpanded Then y = y + 92"
    AppendLine s, "    y = y + 8"
    AppendLine s, "    btnCoastHeader.Top = y"
    AppendLine s, "    btnCoastHeader.Caption = IIf(coastExpanded, ""- Coastline Tools"", ""+ Coastline Tools"")"
    AppendLine s, "    y = y + 24"
    AppendLine s, "    fraCoast.Top = y"
    AppendLine s, "    fraCoast.Visible = coastExpanded"
    AppendLine s, "    If coastExpanded Then y = y + 46"
    AppendLine s, "    y = y + 8"
    AppendLine s, "    btnLabelHeader.Top = y"
    AppendLine s, "    btnLabelHeader.Caption = IIf(labelExpanded, ""- Label Tools"", ""+ Label Tools"")"
    AppendLine s, "    y = y + 24"
    AppendLine s, "    fraLabel.Top = y"
    AppendLine s, "    fraLabel.Visible = labelExpanded"
    AppendLine s, "    If labelExpanded Then y = y + 46"
    AppendLine s, "    y = y + 8"
    AppendLine s, "    btnClose.Top = y"
    AppendLine s, "    Me.Height = y + 54"
    AppendLine s, "End Sub"

    GetFormCode = s
End Function

Private Sub AppendLine(ByRef target As String, ByVal lineText As String)
    target = target & lineText & vbCrLf
End Sub
