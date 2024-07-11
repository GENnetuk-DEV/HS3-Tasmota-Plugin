''' A simple class for setting and getting different settings from the ini file
''' This can of course be done directly to/from the INI file, but having a custom class makes the programming simpler.
Public Class Settings

    Public Sub New()
        Me.Load()
    End Sub

    Public Sub Load()
        Debug(4, "SETTINGS", "Read Config from INI FIle ")

        Me.BrokerIPAddress = hs.GetINISetting("Settings", "MQTTBroker", "127.0.0.1", INIFILE)     'Default value is a refresh every minute
        Me.BrokerUsername = hs.GetINISetting("Settings", "MQTTUsername", "", INIFILE)         'I Like it When I can Set a Default location myself
        Me.BrokerPassword = hs.GetINISetting("Settings", "MQTTPassword", "", INIFILE)
        Me.IniDebugLevel = hs.GetINISetting("Settings", "DebugLevel", 5, INIFILE)
        Me.RawLogFile = hs.GetINISetting("Settings", "Logfile", "", INIFILE)
        GlobalVariables.USID = New Guid(hs.GetINISetting("Settings", "USID", Guid.Empty.ToString, INIFILE))
        GlobalVariables.DoUpdate = hs.GetINISetting("Settings", "DoUpdate", False, INIFILE)
        If GlobalVariables.USID = Guid.Empty Then
            Debug(4, "SETTINGS", "Missing USID Generating")
            GlobalVariables.USID = Guid.NewGuid()
            hs.SaveINISetting("Settings", "USID", GlobalVariables.USID.ToString, INIFILE)
        End If
        'Me.IniDebugLevel = 2
        GetRelease()

    End Sub

    Public Sub Save()
        Debug(4, "SETTINGS", "Write Config to INI FIle ")

        hs.SaveINISetting("Settings", "MQTTBroker", Me.BrokerIPAddress, INIFILE)
        hs.SaveINISetting("Settings", "MQTTUsername", Me.BrokerUsername, INIFILE)
        hs.SaveINISetting("Settings", "MQTTPassword", Me.BrokerPassword, INIFILE)
        hs.SaveINISetting("Settings", "Logfile", Me.RawLogFile, INIFILE)
        hs.SaveINISetting("Settings", "DoUpdate", GlobalVariables.DoUpdate, INIFILE)
    End Sub

    Private _BrokerIPAddress As String
    Public Property BrokerIPAddress() As String
        Get
            Return _BrokerIPAddress
        End Get
        Set(ByVal value As String)
            _BrokerIPAddress = value
        End Set
    End Property

    Private _BrokerUserName As String
    Public Property BrokerUsername() As String
        Get
            Return _BrokerUserName
        End Get
        Set(ByVal value As String)
            _BrokerUserName = value
        End Set
    End Property

    Private _BrokerPassword As String
    Public Property BrokerPassword() As String
        Get
            Return _BrokerPassword
        End Get
        Set(ByVal value As String)
            _BrokerPassword = value
        End Set
    End Property

    Private _IniDebugLevel As Int16
    Public Property IniDebugLevel() As Int16
        Get
            Return _IniDebugLevel
        End Get
        Set(ByVal value As Int16)
            _IniDebugLevel = value
        End Set
    End Property

    Private _RawLogFIle As String
    Public Property RawLogFile() As String
        Get
            Return _RawLogFIle
        End Get
        Set(ByVal value As String)
            _RawLogFIle = value
        End Set
    End Property

End Class
