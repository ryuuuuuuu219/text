Attribute VB_Name = "MapToolsASCII"
Option Explicit

' ============================================================
' Excel cell-map editing tools
' 1. Duplicate every selected row and column to double resolution
' 2. Randomly roughen a coastline inside the selected rectangle
' 3. Insert a text box over the selected range
' ============================================================

Public Sub DoubleMapResolution()
    Dim target As Range
    Dim ws As Worksheet
    Dim firstRow As Long, lastRow As Long
    Dim firstCol As Long, lastCol As Long
    Dim originalRowCount As Long, originalColCount As Long
    Dim r As Long, c As Long
    Dim oldCalc As XlCalculation

    If TypeName(Selection) <> "Range" Then
        MsgBox "Select the rectangular map range first.", vbExclamation
        Exit Sub
    End If

    Set target = Selection
    Set ws = target.Worksheet

    If target.Areas.Count > 1 Then
        MsgBox "Select one continuous rectangular range.", vbExclamation
        Exit Sub
    End If

    If target.MergeCells Then
        MsgBox "The selected range contains merged cells. Unmerge them first.", vbExclamation
        Exit Sub
    End If

    If MsgBox( _
        "Duplicate every row and column in " & target.Address(False, False) & _
        " to double the map resolution?" & vbCrLf & _
        "Cells outside the range will move because rows and columns are inserted.", _
        vbYesNo + vbQuestion, "Double Map Resolution") <> vbYes Then Exit Sub

    firstRow = target.Row
    lastRow = target.Row + target.Rows.Count - 1
    firstCol = target.Column
    lastCol = target.Column + target.Columns.Count - 1
    originalRowCount = target.Rows.Count
    originalColCount = target.Columns.Count

    oldCalc = Application.Calculation
    On Error GoTo ErrorHandler

    Application.ScreenUpdating = False
    Application.EnableEvents = False
    Application.Calculation = xlCalculationManual

    ' Work from bottom to top so original row positions remain valid.
    For r = lastRow To firstRow Step -1
        ws.Rows(r + 1).Insert Shift:=xlDown, CopyOrigin:=xlFormatFromLeftOrAbove
        ws.Rows(r).Copy Destination:=ws.Rows(r + 1)
        ws.Rows(r + 1).RowHeight = ws.Rows(r).RowHeight
    Next r

    ' Work from right to left after row duplication.
    For c = lastCol To firstCol Step -1
        ws.Columns(c + 1).Insert Shift:=xlToRight, CopyOrigin:=xlFormatFromLeftOrAbove
        ws.Columns(c).Copy Destination:=ws.Columns(c + 1)
        ws.Columns(c + 1).ColumnWidth = ws.Columns(c).ColumnWidth
    Next c

    Application.CutCopyMode = False

    ws.Range( _
        ws.Cells(firstRow, firstCol), _
        ws.Cells(firstRow + originalRowCount * 2 - 1, _
                 firstCol + originalColCount * 2 - 1) _
    ).Select

    MsgBox "Map resolution was doubled.", vbInformation

CleanExit:
    Application.ScreenUpdating = True
    Application.EnableEvents = True
    Application.Calculation = oldCalc
    Exit Sub

ErrorHandler:
    MsgBox "An error occurred:" & vbCrLf & Err.Description, vbCritical
    Resume CleanExit
End Sub

Public Sub RoughenCoastline()
    Dim target As Range
    Dim seaSample As Range
    Dim ws As Worksheet
    Dim intensityInput As Variant
    Dim intensity As Double
    Dim rowsCount As Long, colsCount As Long
    Dim isLand() As Boolean
    Dim changeType() As Integer
    Dim sourceRow() As Long, sourceCol() As Long
    Dim r As Long, c As Long
    Dim landNeighbors As Long, seaNeighbors As Long
    Dim picked As Long
    Dim currentCell As Range
    Dim oldCalc As XlCalculation

    If TypeName(Selection) <> "Range" Then
        MsgBox "Select the rectangular coastline area first.", vbExclamation
        Exit Sub
    End If

    Set target = Selection
    Set ws = target.Worksheet

    If target.Areas.Count > 1 Then
        MsgBox "Select one continuous rectangular range.", vbExclamation
        Exit Sub
    End If

    If target.MergeCells Then
        MsgBox "The selected range contains merged cells. Unmerge them first.", vbExclamation
        Exit Sub
    End If

    On Error Resume Next
    Set seaSample = Application.InputBox( _
        Prompt:="Select one sample cell that represents sea.", _
        Title:="Select Sea Cell", Type:=8)
    On Error GoTo 0

    If seaSample Is Nothing Then Exit Sub
    Set seaSample = seaSample.Cells(1, 1)

    intensityInput = Application.InputBox( _
        Prompt:="Enter roughness from 0 to 100." & vbCrLf & _
                "Recommended: 15 to 35", _
        Title:="Coastline Roughness", Default:=25, Type:=1)

    If VarType(intensityInput) = vbBoolean And intensityInput = False Then Exit Sub

    intensity = CDbl(intensityInput)
    If intensity < 0 Or intensity > 100 Then
        MsgBox "Enter a value from 0 to 100.", vbExclamation
        Exit Sub
    End If
    intensity = intensity / 100#

    rowsCount = target.Rows.Count
    colsCount = target.Columns.Count

    ReDim isLand(1 To rowsCount, 1 To colsCount)
    ReDim changeType(1 To rowsCount, 1 To colsCount)
    ReDim sourceRow(1 To rowsCount, 1 To colsCount)
    ReDim sourceCol(1 To rowsCount, 1 To colsCount)

    oldCalc = Application.Calculation
    On Error GoTo ErrorHandler

    Application.ScreenUpdating = False
    Application.EnableEvents = False
    Application.Calculation = xlCalculationManual
    Randomize

    ' Record the original state before deciding any changes.
    For r = 1 To rowsCount
        For c = 1 To colsCount
            Set currentCell = target.Cells(r, c)
            isLand(r, c) = Not SameCellAppearance(currentCell, seaSample)
        Next c
    Next r

    ' Decide all changes first to prevent chain reactions in one pass.
    For r = 1 To rowsCount
        For c = 1 To colsCount
            CountNeighbors r, c, rowsCount, colsCount, isLand, landNeighbors, seaNeighbors

            If isLand(r, c) Then
                If seaNeighbors > 0 Then
                    If Rnd < intensity * (seaNeighbors / 8#) Then
                        changeType(r, c) = -1
                    End If
                End If
            Else
                If landNeighbors > 0 Then
                    If Rnd < intensity * (landNeighbors / 8#) Then
                        changeType(r, c) = 1
                        picked = PickRandomLandNeighbor( _
                            r, c, rowsCount, colsCount, isLand, _
                            sourceRow(r, c), sourceCol(r, c))
                        If picked = 0 Then changeType(r, c) = 0
                    End If
                End If
            End If
        Next c
    Next r

    ' Apply the prepared changes.
    For r = 1 To rowsCount
        For c = 1 To colsCount
            Select Case changeType(r, c)
                Case -1
                    CopyCellAppearance seaSample, target.Cells(r, c)
                Case 1
                    CopyCellAppearance _
                        target.Cells(sourceRow(r, c), sourceCol(r, c)), _
                        target.Cells(r, c)
            End Select
        Next c
    Next r

    Application.CutCopyMode = False
    MsgBox "The coastline was roughened. Run again if more variation is needed.", vbInformation

CleanExit:
    Application.ScreenUpdating = True
    Application.EnableEvents = True
    Application.Calculation = oldCalc
    Exit Sub

ErrorHandler:
    MsgBox "An error occurred:" & vbCrLf & Err.Description, vbCritical
    Resume CleanExit
End Sub

Public Sub InsertMapTextBox()
    Dim target As Range
    Dim ws As Worksheet
    Dim inputText As Variant
    Dim shp As Shape
    Dim boxLeft As Double, boxTop As Double
    Dim boxWidth As Double, boxHeight As Double

    If TypeName(Selection) <> "Range" Then
        MsgBox "Select the cell range for the text box first.", vbExclamation
        Exit Sub
    End If

    Set target = Selection
    Set ws = target.Worksheet

    If target.Areas.Count > 1 Then
        MsgBox "Select one continuous rectangular range.", vbExclamation
        Exit Sub
    End If

    inputText = Application.InputBox( _
        Prompt:="Enter the label text.", _
        Title:="Map Label", Type:=2)

    If VarType(inputText) = vbBoolean And inputText = False Then Exit Sub
    If Len(CStr(inputText)) = 0 Then Exit Sub

    boxLeft = target.Left
    boxTop = target.Top

    If target.Cells.CountLarge = 1 Then
        boxWidth = 120
        boxHeight = 36
    Else
        boxWidth = target.Width
        boxHeight = target.Height
    End If

    Set shp = ws.Shapes.AddTextbox( _
        Orientation:=msoTextOrientationHorizontal, _
        Left:=boxLeft, Top:=boxTop, Width:=boxWidth, Height:=boxHeight)

    With shp
        .Name = UniqueShapeName(ws, "MapLabel")
        .Placement = xlMoveAndSize
        .Fill.Visible = msoFalse
        .Line.Visible = msoFalse

        With .TextFrame2
            .TextRange.Text = CStr(inputText)
            .TextRange.Font.Name = "Arial"
            .TextRange.Font.Size = 14
            .TextRange.Font.Bold = msoTrue
            .TextRange.ParagraphFormat.Alignment = msoAlignCenter
            .VerticalAnchor = msoAnchorMiddle
            .MarginLeft = 2
            .MarginRight = 2
            .MarginTop = 1
            .MarginBottom = 1
            .AutoSize = msoAutoSizeTextToFitShape
        End With
    End With

    shp.Select
End Sub

Private Function SameCellAppearance(ByVal a As Range, ByVal b As Range) As Boolean
    SameCellAppearance = _
        (a.Interior.Pattern = b.Interior.Pattern) And _
        (a.Interior.Color = b.Interior.Color) And _
        (a.Interior.TintAndShade = b.Interior.TintAndShade)
End Function

Private Sub CopyCellAppearance(ByVal sourceCell As Range, ByVal destinationCell As Range)
    sourceCell.Copy
    destinationCell.PasteSpecial Paste:=xlPasteFormats
End Sub

Private Sub CountNeighbors( _
    ByVal r As Long, ByVal c As Long, _
    ByVal maxRow As Long, ByVal maxCol As Long, _
    ByRef isLand() As Boolean, _
    ByRef landNeighbors As Long, ByRef seaNeighbors As Long)

    Dim dr As Long, dc As Long
    Dim nr As Long, nc As Long

    landNeighbors = 0
    seaNeighbors = 0

    For dr = -1 To 1
        For dc = -1 To 1
            If Not (dr = 0 And dc = 0) Then
                nr = r + dr
                nc = c + dc

                If nr >= 1 And nr <= maxRow And nc >= 1 And nc <= maxCol Then
                    If isLand(nr, nc) Then
                        landNeighbors = landNeighbors + 1
                    Else
                        seaNeighbors = seaNeighbors + 1
                    End If
                End If
            End If
        Next dc
    Next dr
End Sub

Private Function PickRandomLandNeighbor( _
    ByVal r As Long, ByVal c As Long, _
    ByVal maxRow As Long, ByVal maxCol As Long, _
    ByRef isLand() As Boolean, _
    ByRef pickedRow As Long, ByRef pickedCol As Long) As Long

    Dim rr(1 To 8) As Long
    Dim cc(1 To 8) As Long
    Dim count As Long
    Dim index As Long
    Dim dr As Long, dc As Long

    count = 0

    For dr = -1 To 1
        For dc = -1 To 1
            If Not (dr = 0 And dc = 0) Then
                If r + dr >= 1 And r + dr <= maxRow And _
                   c + dc >= 1 And c + dc <= maxCol Then
                    If isLand(r + dr, c + dc) Then
                        count = count + 1
                        rr(count) = r + dr
                        cc(count) = c + dc
                    End If
                End If
            End If
        Next dc
    Next dr

    If count = 0 Then
        PickRandomLandNeighbor = 0
        Exit Function
    End If

    index = Int(Rnd * count) + 1
    pickedRow = rr(index)
    pickedCol = cc(index)
    PickRandomLandNeighbor = 1
End Function

Private Function UniqueShapeName(ByVal ws As Worksheet, ByVal baseName As String) As String
    Dim n As Long
    Dim candidate As String
    Dim testShape As Shape

    n = 1
    Do
        candidate = baseName & n
        Set testShape = Nothing
        On Error Resume Next
        Set testShape = ws.Shapes(candidate)
        On Error GoTo 0

        If testShape Is Nothing Then
            UniqueShapeName = candidate
            Exit Function
        End If

        n = n + 1
    Loop
End Function

Public Sub ReduceRowResolution()
    Dim target As Range
    Dim ws As Worksheet
    Dim firstRow As Long, lastRow As Long
    Dim rowCount As Long
    Dim r As Long
    Dim oldCalc As XlCalculation

    If TypeName(Selection) <> "Range" Then
        MsgBox "Select the map range first.", vbExclamation
        Exit Sub
    End If

    Set target = Selection
    Set ws = target.Worksheet

    If target.Areas.Count > 1 Then
        MsgBox "Select one continuous rectangular range.", vbExclamation
        Exit Sub
    End If

    If target.MergeCells Then
        MsgBox "The selected range contains merged cells. Unmerge them first.", vbExclamation
        Exit Sub
    End If

    rowCount = target.Rows.Count
    If rowCount < 2 Then
        MsgBox "Select at least two rows.", vbExclamation
        Exit Sub
    End If

    If rowCount Mod 2 <> 0 Then
        MsgBox "The selected range must contain an even number of rows.", vbExclamation
        Exit Sub
    End If

    If MsgBox( _
        "Delete the second row of every pair in " & target.Address(False, False) & "?" & vbCrLf & _
        "This reduces vertical resolution by half and deletes entire worksheet rows.", _
        vbYesNo + vbQuestion, "Reduce Row Resolution") <> vbYes Then Exit Sub

    firstRow = target.Row
    lastRow = firstRow + rowCount - 1

    oldCalc = Application.Calculation
    On Error GoTo ErrorHandler

    Application.ScreenUpdating = False
    Application.EnableEvents = False
    Application.Calculation = xlCalculationManual

    ' Delete the second row of each pair, working from bottom to top.
    For r = lastRow To firstRow + 1 Step -2
        ws.Rows(r).Delete Shift:=xlUp
    Next r

    ws.Range( _
        ws.Cells(firstRow, target.Column), _
        ws.Cells(firstRow + rowCount \ 2 - 1, target.Column + target.Columns.Count - 1) _
    ).Select

    MsgBox "Vertical resolution was reduced by half.", vbInformation

CleanExit:
    Application.ScreenUpdating = True
    Application.EnableEvents = True
    Application.Calculation = oldCalc
    Exit Sub

ErrorHandler:
    MsgBox "An error occurred:" & vbCrLf & Err.Description, vbCritical
    Resume CleanExit
End Sub

Public Sub ReduceColumnResolution()
    Dim target As Range
    Dim ws As Worksheet
    Dim firstCol As Long, lastCol As Long
    Dim colCount As Long
    Dim c As Long
    Dim oldCalc As XlCalculation

    If TypeName(Selection) <> "Range" Then
        MsgBox "Select the map range first.", vbExclamation
        Exit Sub
    End If

    Set target = Selection
    Set ws = target.Worksheet

    If target.Areas.Count > 1 Then
        MsgBox "Select one continuous rectangular range.", vbExclamation
        Exit Sub
    End If

    If target.MergeCells Then
        MsgBox "The selected range contains merged cells. Unmerge them first.", vbExclamation
        Exit Sub
    End If

    colCount = target.Columns.Count
    If colCount < 2 Then
        MsgBox "Select at least two columns.", vbExclamation
        Exit Sub
    End If

    If colCount Mod 2 <> 0 Then
        MsgBox "The selected range must contain an even number of columns.", vbExclamation
        Exit Sub
    End If

    If MsgBox( _
        "Delete the second column of every pair in " & target.Address(False, False) & "?" & vbCrLf & _
        "This reduces horizontal resolution by half and deletes entire worksheet columns.", _
        vbYesNo + vbQuestion, "Reduce Column Resolution") <> vbYes Then Exit Sub

    firstCol = target.Column
    lastCol = firstCol + colCount - 1

    oldCalc = Application.Calculation
    On Error GoTo ErrorHandler

    Application.ScreenUpdating = False
    Application.EnableEvents = False
    Application.Calculation = xlCalculationManual

    ' Delete the second column of each pair, working from right to left.
    For c = lastCol To firstCol + 1 Step -2
        ws.Columns(c).Delete Shift:=xlToLeft
    Next c

    ws.Range( _
        ws.Cells(target.Row, firstCol), _
        ws.Cells(target.Row + target.Rows.Count - 1, firstCol + colCount \ 2 - 1) _
    ).Select

    MsgBox "Horizontal resolution was reduced by half.", vbInformation

CleanExit:
    Application.ScreenUpdating = True
    Application.EnableEvents = True
    Application.Calculation = oldCalc
    Exit Sub

ErrorHandler:
    MsgBox "An error occurred:" & vbCrLf & Err.Description, vbCritical
    Resume CleanExit
End Sub
