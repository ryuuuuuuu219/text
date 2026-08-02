VERSION 5.00
Begin VB.UserForm MapToolMenu 
   Caption         =   "Map Tools"
   ClientHeight    =   6180
   ClientLeft      =   120
   ClientTop       =   465
   ClientWidth     =   5160
   ShowModal       =   0   'False
   StartUpPosition =   1  'CenterOwner
   Begin MSForms.Label lblSelection
      Caption         =   "Selection: none"
      Height          =   300
      Left            =   180
      TabIndex        =   0
      Top             =   180
      Width           =   4680
   End
   Begin MSForms.CommandButton btnRefresh
      Caption         =   "Refresh Selection"
      Height          =   420
      Left            =   180
      TabIndex        =   1
      Top             =   540
      Width           =   4680
   End
   Begin MSForms.CommandButton btnResolutionHeader
      Caption         =   "- Resolution Tools"
      Height          =   420
      Left            =   180
      TabIndex        =   2
      Top             =   1080
      Width           =   4680
   End
   Begin MSForms.Frame fraResolution
      Caption         =   ""
      Height          =   1500
      Left            =   180
      TabIndex        =   3
      Top             =   1500
      Width           =   4680
      Begin MSForms.CommandButton btnDouble
         Caption         =   "Double Rows and Columns"
         Height          =   390
         Left            =   180
         TabIndex        =   4
         Top             =   180
         Width           =   4260
      End
      Begin MSForms.CommandButton btnReduceRows
         Caption         =   "Reduce Rows by Half"
         Height          =   390
         Left            =   180
         TabIndex        =   5
         Top             =   600
         Width           =   4260
      End
      Begin MSForms.CommandButton btnReduceColumns
         Caption         =   "Reduce Columns by Half"
         Height          =   390
         Left            =   180
         TabIndex        =   6
         Top             =   1020
         Width           =   4260
      End
   End
   Begin MSForms.CommandButton btnCoastHeader
      Caption         =   "+ Coastline Tools"
      Height          =   420
      Left            =   180
      TabIndex        =   7
      Top             =   3120
      Width           =   4680
   End
   Begin MSForms.Frame fraCoast
      Caption         =   ""
      Height          =   660
      Left            =   180
      TabIndex        =   8
      Top             =   3540
      Visible         =   0   'False
      Width           =   4680
      Begin MSForms.CommandButton btnRoughen
         Caption         =   "Roughen Selected Coastline"
         Height          =   390
         Left            =   180
         TabIndex        =   9
         Top             =   150
         Width           =   4260
      End
   End
   Begin MSForms.CommandButton btnLabelHeader
      Caption         =   "+ Label Tools"
      Height          =   420
      Left            =   180
      TabIndex        =   10
      Top             =   4320
      Width           =   4680
   End
   Begin MSForms.Frame fraLabel
      Caption         =   ""
      Height          =   660
      Left            =   180
      TabIndex        =   11
      Top             =   4740
      Visible         =   0   'False
      Width           =   4680
      Begin MSForms.CommandButton btnTextBox
         Caption         =   "Insert Text Box"
         Height          =   390
         Left            =   180
         TabIndex        =   12
         Top             =   150
         Width           =   4260
      End
   End
   Begin MSForms.CommandButton btnClose
      Caption         =   "Close"
      Height          =   420
      Left            =   180
      TabIndex        =   13
      Top             =   5520
      Width           =   4680
   End
End
Attribute VB_Name = "MapToolMenu"
Attribute VB_GlobalNameSpace = False
Attribute VB_Creatable = False
Attribute VB_PredeclaredId = True
Attribute VB_Exposed = False
Option Explicit

Private resolutionExpanded As Boolean
Private coastExpanded As Boolean
Private labelExpanded As Boolean

Private Const FORM_WIDTH As Single = 5160
Private Const TOP_MARGIN As Single = 180
Private Const LEFT_MARGIN As Single = 180
Private Const CONTROL_WIDTH As Single = 4680
Private Const HEADER_HEIGHT As Single = 420
Private Const GAP As Single = 120
Private Const RESOLUTION_HEIGHT As Single = 1500
Private Const SINGLE_HEIGHT As Single = 660
Private Const BOTTOM_PADDING As Single = 420

Private Sub UserForm_Initialize()
    resolutionExpanded = True
    coastExpanded = False
    labelExpanded = False
    RefreshSelectionText
    LayoutForm
End Sub

Private Sub UserForm_Activate()
    RefreshSelectionText
End Sub

Private Sub btnRefresh_Click()
    RefreshSelectionText
End Sub

Private Sub btnResolutionHeader_Click()
    resolutionExpanded = Not resolutionExpanded
    LayoutForm
End Sub

Private Sub btnCoastHeader_Click()
    coastExpanded = Not coastExpanded
    LayoutForm
End Sub

Private Sub btnLabelHeader_Click()
    labelExpanded = Not labelExpanded
    LayoutForm
End Sub

Private Sub btnDouble_Click()
    DoubleMapResolution
    RefreshSelectionText
End Sub

Private Sub btnReduceRows_Click()
    ReduceRowResolution
    RefreshSelectionText
End Sub

Private Sub btnReduceColumns_Click()
    ReduceColumnResolution
    RefreshSelectionText
End Sub

Private Sub btnRoughen_Click()
    RoughenCoastline
    RefreshSelectionText
End Sub

Private Sub btnTextBox_Click()
    InsertMapTextBox
    RefreshSelectionText
End Sub

Private Sub btnClose_Click()
    Unload Me
End Sub

Private Sub RefreshSelectionText()
    On Error GoTo NoSelection

    If TypeName(Selection) = "Range" Then
        lblSelection.Caption = "Selection: " & Selection.Worksheet.Name & "!" & _
                               Selection.Address(False, False) & _
                               "  (" & Selection.Rows.Count & " x " & Selection.Columns.Count & ")"
    Else
        lblSelection.Caption = "Selection: none"
    End If
    Exit Sub

NoSelection:
    lblSelection.Caption = "Selection: unavailable"
End Sub

Private Sub LayoutForm()
    Dim nextTop As Single

    Me.Width = FORM_WIDTH
    nextTop = TOP_MARGIN

    lblSelection.Left = LEFT_MARGIN
    lblSelection.Top = nextTop
    lblSelection.Width = CONTROL_WIDTH
    nextTop = nextTop + lblSelection.Height + GAP

    btnRefresh.Left = LEFT_MARGIN
    btnRefresh.Top = nextTop
    btnRefresh.Width = CONTROL_WIDTH
    nextTop = nextTop + btnRefresh.Height + GAP

    btnResolutionHeader.Left = LEFT_MARGIN
    btnResolutionHeader.Top = nextTop
    btnResolutionHeader.Width = CONTROL_WIDTH
    btnResolutionHeader.Caption = IIf(resolutionExpanded, "- Resolution Tools", "+ Resolution Tools")
    nextTop = nextTop + HEADER_HEIGHT

    fraResolution.Left = LEFT_MARGIN
    fraResolution.Top = nextTop
    fraResolution.Width = CONTROL_WIDTH
    fraResolution.Visible = resolutionExpanded
    If resolutionExpanded Then nextTop = nextTop + RESOLUTION_HEIGHT
    nextTop = nextTop + GAP

    btnCoastHeader.Left = LEFT_MARGIN
    btnCoastHeader.Top = nextTop
    btnCoastHeader.Width = CONTROL_WIDTH
    btnCoastHeader.Caption = IIf(coastExpanded, "- Coastline Tools", "+ Coastline Tools")
    nextTop = nextTop + HEADER_HEIGHT

    fraCoast.Left = LEFT_MARGIN
    fraCoast.Top = nextTop
    fraCoast.Width = CONTROL_WIDTH
    fraCoast.Visible = coastExpanded
    If coastExpanded Then nextTop = nextTop + SINGLE_HEIGHT
    nextTop = nextTop + GAP

    btnLabelHeader.Left = LEFT_MARGIN
    btnLabelHeader.Top = nextTop
    btnLabelHeader.Width = CONTROL_WIDTH
    btnLabelHeader.Caption = IIf(labelExpanded, "- Label Tools", "+ Label Tools")
    nextTop = nextTop + HEADER_HEIGHT

    fraLabel.Left = LEFT_MARGIN
    fraLabel.Top = nextTop
    fraLabel.Width = CONTROL_WIDTH
    fraLabel.Visible = labelExpanded
    If labelExpanded Then nextTop = nextTop + SINGLE_HEIGHT
    nextTop = nextTop + GAP

    btnClose.Left = LEFT_MARGIN
    btnClose.Top = nextTop
    btnClose.Width = CONTROL_WIDTH
    nextTop = nextTop + btnClose.Height + BOTTOM_PADDING

    Me.Height = nextTop
End Sub
