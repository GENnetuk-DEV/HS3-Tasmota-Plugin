Module classes

    ''' <summary>
    ''' A custom dictionary for storing serializable objects
    ''' </summary>
    ''' <remarks></remarks>
    <Serializable()>
    Public Class hsCollection
        Inherits Dictionary(Of String, Object)
        Dim KeyIndex As New Collection

        Public Sub New()
            MyBase.New()
        End Sub

        Protected Sub New(ByVal info As System.Runtime.Serialization.SerializationInfo, ByVal context As System.Runtime.Serialization.StreamingContext)
            MyBase.New(info, context)
        End Sub

        Public Overloads Sub Add(value As Object, Key As String)
            If Not MyBase.ContainsKey(Key) Then
                MyBase.Add(Key, value)
                KeyIndex.Add(Key, Key)
            Else
                MyBase.Item(Key) = value
            End If
        End Sub

        Public Overloads Sub Remove(Key As String)
            On Error Resume Next
            MyBase.Remove(Key)
            KeyIndex.Remove(Key)
        End Sub

        Public Overloads Sub Remove(Index As Integer)
            MyBase.Remove(KeyIndex(Index))
            KeyIndex.Remove(Index)
        End Sub

        Public Overloads ReadOnly Property Keys(ByVal index As Integer) As Object
            Get
                Dim i As Integer
                Dim key As String = Nothing
                For Each key In MyBase.Keys
                    If i = index Then
                        Exit For
                    Else
                        i += 1
                    End If
                Next
                Return key
            End Get
        End Property

        Default Public Overloads Property Item(ByVal index As Integer) As Object
            Get
                Return MyBase.Item(KeyIndex(index))
            End Get
            Set(ByVal value As Object)
                MyBase.Item(KeyIndex(index)) = value
            End Set
        End Property

        Default Public Overloads Property Item(ByVal Key As String) As Object
            Get
                On Error Resume Next
                Return MyBase.Item(Key)
            End Get
            Set(ByVal value As Object)
                If Not MyBase.ContainsKey(Key) Then
                    Add(value, Key)
                Else
                    MyBase.Item(Key) = value
                End If
            End Set
        End Property
    End Class

    <Serializable()>
    Public Class Action
        Inherits hsCollection
        Public Sub New()
            MyBase.New()
        End Sub
        Protected Sub New(ByVal info As System.Runtime.Serialization.SerializationInfo, ByVal context As System.Runtime.Serialization.StreamingContext)
            MyBase.New(info, context)
        End Sub
    End Class

    <Serializable()>
    Public Class Trigger
        Inherits hsCollection

        Public Sub New()
            MyBase.New()
        End Sub
        Protected Sub New(ByVal info As System.Runtime.Serialization.SerializationInfo, ByVal context As System.Runtime.Serialization.StreamingContext)
            MyBase.New(info, context)
        End Sub
    End Class

    ''' <summary>
    ''' HSPI_SAMPE_BASIC class
    ''' </summary>
    <Serializable()>
    Public Class SampleClass
        Dim sHousecode As String
        Dim sDeviceCode As String
        Public Property houseCode() As String
            Get
                Return sHousecode
            End Get
            Set(ByVal value As String)
                sHousecode = value
            End Set
        End Property
        Public Property deviceCode() As String
            Get
                Return sDeviceCode
            End Get
            Set(ByVal value As String)
                sDeviceCode = value
            End Set
        End Property
    End Class

    Public Class GlobalVariables
        Public Shared ModuleName As String = "TasMQTT"
        Public Shared USID As Guid
        'Public Shared MQTTDevices As New Dictionary(Of Integer, String)
        Public Shared MQTTQ As New Queue()
        Public Shared DebugLevel As Int16 = 0
        Public Shared IgnoredEvents As Double = 0
        Public Shared TasmotaDevices As New List(Of DeviceInfo)
        Public Shared Warnings As New List(Of Warnings)
        Public Shared EnableQueue As Boolean = False
        Public Shared MQTTConnected As Boolean = False

        Public Shared RawLog As System.IO.StreamWriter
        Public Shared RawLogFileName As String = "None"
        Public Shared LogFile As Boolean = False
        Public Shared IsShuttingDown As Boolean = False

        Public Shared LatestVersion As String = ""
        Public Shared ReleaseNotes As String = ""
        Public Shared DoUpdate As Boolean = False

        'Statistics
        Public Shared StatsqProcessed As ULong = 0  'Queue Events Processed
        Public Shared StatsPublishes As ULong = 0   'MQTT Publishes
        Public Shared StatsMessages As ULong = 0    'MQTT Subscriptions
        Public Shared StatsHSEvents As ULong = 0    'HS Events


    End Class

    Public Class Warnings
        Dim _TimeStamp As DateTime
        Dim _Level As Int16
        Dim _Warning As String

        Public Property TimeStamp As DateTime
            Get
                Return _TimeStamp
            End Get
            Set(value As DateTime)
                _TimeStamp = value
            End Set
        End Property
        Public Property Level As Int16
            Get
                Return _Level
            End Get
            Set(value As Int16)
                _Level = value
            End Set
        End Property

        Public Property Warning
            Get
                Return _Warning
            End Get
            Set(value)
                _Warning = value
            End Set
        End Property

    End Class
    Public Class DeviceInfo
        Dim _DeviceID As UInteger
        Dim _DeviceString As String
        Dim _DeviceName As String
        Dim _HSDeviceName As String
        Dim _DeviceTarget As String
        Dim _Active As Boolean
        Dim _LastSeen As Date
        Dim _RSSI As UInteger
        Dim _Type As UInt16
        Dim _UpTime As Long
        Dim _Pending As Boolean
        Dim _IgnoreTele As Boolean


        Public Property DeviceID As String
            Get
                Return _DeviceID
            End Get
            Set(ByVal value As String)
                _DeviceID = value
            End Set
        End Property

        Public Property DeviceString As String
            Get
                Return _DeviceString
            End Get
            Set(ByVal value As String)
                _DeviceString = value
            End Set
        End Property

        Public Property DeviceName As String
            Get
                Return _DeviceName
            End Get
            Set(ByVal value As String)
                _DeviceName = value
            End Set
        End Property
        Public Property HSDeviceName As String
            Get
                Return _HSDeviceName
            End Get
            Set(ByVal value As String)
                _HSDeviceName = value
            End Set
        End Property

        Public Property DeviceTarget As String
            Get
                Return _DeviceTarget
            End Get
            Set(ByVal value As String)
                _DeviceTarget = value
            End Set
        End Property
        Public Property Active As String
            Get
                Return _Active
            End Get
            Set(ByVal value As String)
                _Active = value
            End Set
        End Property

        Public Property LastSeen As String
            Get
                Return _LastSeen
            End Get
            Set(ByVal value As String)
                _LastSeen = value
            End Set
        End Property
        Public Property RSSI As String
            Get
                Return _RSSI
            End Get
            Set(ByVal value As String)
                _RSSI = value
            End Set
        End Property
        Public Property Type As String
            Get
                Return _Type
            End Get
            Set(ByVal value As String)
                _Type = value
            End Set
        End Property
        Public Property Uptime As String
            Get
                Return _UpTime
            End Get
            Set(ByVal value As String)
                _UpTime = value
            End Set
        End Property
        Public Property Pending As String
            Get
                Return _Pending
            End Get
            Set(ByVal value As String)
                _Pending = value
            End Set
        End Property
        Public Property IgnoreTele As Boolean
            Get
                Return _IgnoreTele
            End Get
            Set(ByVal value As Boolean)
                _IgnoreTele = value
            End Set
        End Property
    End Class

End Module

