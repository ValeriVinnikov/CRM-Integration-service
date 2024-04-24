using CRM_Integration_service.Models;
using o2g;
using o2g.Events.Telephony;
using o2g.Events;
using o2g.Types.TelephonyNS.CallNS;
using System.Dynamic;
using System.Net.Http;
using o2g.Types.TelephonyNS;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Text;
using System.Xml.Serialization;

namespace CRM_Integration_service
{
  public class Worker : BackgroundService
  {
    private readonly ILogger<Worker> _logger;

    /// <summary>
    /// Declaration of global variables
    /// </summary>
    string _telephonyServerHost; //O2G host
    string _adminName;
    string _adminPassword;
    string _crmUrl;
    string _dashboardUrl;
    string _aPiuserPwd;
    string _aPiuser;
    string _helpDeskUrl;
    string _usersFileName; //Name of XML file where user name and extensions are stored
    string _logFileName = "";
    string incomingCallRef = "";
    string outgoingCallRef = "";
    string[] loginNames;
    bool announcementMade = false;
    List<o2g.Types.TelephonyNS.PbxCall> calls;
    private readonly IHttpClientFactory _httpClientFactory = null!; //Used to create and maintain HTTPClient for making http requests to dashboard
    Dictionary<string, OngoingCallInfo> ongoingCalls = new Dictionary<string, OngoingCallInfo>(); //Collection of informations about ongoing calls. Each information is addressed by Call reference which is unique string

    Dictionary<string, UserData> usersData = new Dictionary<string, UserData>(); //Collection of all parameters of each user. Each information is addressed by user login name

    // Create new O2G applicaion
    private O2G.Application _o2gApp = new("CrmIntegrationService");


    public Worker(ILogger<Worker> logger, IHttpClientFactory httpClientFactory)
    {
      _logger = logger;
      _httpClientFactory = httpClientFactory;
      LoadApplictionSettings();
      LoadUsersFromFile();
      //Login of administrator
      Task<bool> _login = Login();
      if (_login.Result)
      {
        _logger.LogInformation("Administrator login into O2G was SUCCESSFUL!!!");
      }
      Task<bool> _subscribe = SubscribeForEvents();
      if (_subscribe.Result)
      {
        _logger.LogInformation("Subscription for events was SUCCESSFUL!!!");
      }
    }

    private void LoadApplictionSettings()
    {
      _telephonyServerHost = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build().GetSection("AppSettings")["O2GServerHost"];
      _adminName = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build().GetSection("AppSettings")["O2GAdminName"];
      _adminPassword = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build().GetSection("AppSettings")["O2GAdminPassword"];
      _usersFileName = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build().GetSection("AppSettings")["UsersFileName"];
      _crmUrl = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build().GetSection("AppSettings")["CrmUrl"];
      _aPiuserPwd = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build().GetSection("AppSettings")["APIuserPwd"];
      _aPiuser = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build().GetSection("AppSettings")["APIuser"];
    }

    /// <summary>
    /// Load list of users from XML file
    /// </summary>
    public void LoadUsersFromFile()
    {
      _logger.LogInformation("VOID LoadUsersFromFile()");
      // Create an instance of the XmlSerializer.
      XmlSerializer serializer = new XmlSerializer(typeof(List<OxeUser>));
      usersData.Clear();
      // Declare an object variable of the type to be deserialized.
      List<OxeUser> _oxeUsers;


      using (Stream reader = System.IO.File.Open(_usersFileName,
                                FileMode.Open,
                                FileAccess.Read,
                                FileShare.ReadWrite))
      {
        // Call the Deserialize method to restore the object's state.
        _oxeUsers = (List<OxeUser>)serializer.Deserialize(reader);
      }

      foreach (OxeUser _oxeUser in _oxeUsers)
      {
        _logger.LogInformation("User: " + _oxeUser.Name);
        UserData _userData = new UserData();
        _userData.UserName = _oxeUser.Name;
        _userData.Extension = _oxeUser.Extension;
        _userData.LoginName = "oxe" + _oxeUser.Extension;
        if (!usersData.ContainsKey(_userData.LoginName))
        {
          usersData.Add(_userData.LoginName, _userData);
        }

      }
      _logger.LogInformation("Collection of OXE users consists of " + usersData.Count);

    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
      while (!stoppingToken.IsCancellationRequested)
      {
        _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
        await Task.Delay(60000, stoppingToken);
      }
    }

    /// <summary>
    /// Admin login function
    /// </summary>
    /// <returns></returns>
    public async Task<bool> Login()
    {
      _logger.LogInformation("Login O2G admin user");
      bool _loginSuccess = false;

      try
      {
        //Set host
        _o2gApp.SetHost(new()
        {
          PrivateAddress = _telephonyServerHost
        }, null);
        // Login to the O2G server
        await _o2gApp.LoginAsync(_adminName, _adminPassword);
        
        //Define event handlers of telephony events 
        this._o2gApp.TelephonyService.CallCreated += TelephonyService_CallCreated;
        this._o2gApp.TelephonyService.CallModified += TelephonyService_CallModified;
        this._o2gApp.TelephonyService.CallRemoved += TelephonyService_CallRemoved;


        //Create of array of user logins for subscription
        int i = 0;
        loginNames = new string[usersData.Count];
        foreach (KeyValuePair<string, UserData> keyValue in usersData)
        {
          UserData _user = keyValue.Value;
          loginNames[i] = _user.LoginName;
          i++;
        }
      }
      catch (Exception ex)
      {
        _logger.LogError("Administrator login into O2G failed due to this exception: " + ex.Message);
      }
      return _loginSuccess;
    }

    public async Task<bool> SubscribeForEvents()
    {
      bool _subscriptionSuccess = false;
      try
      {
        //Build subscription
        Subscription s = Subscription.Builder
            .AddTelephonyEvents(loginNames)
            //.AddCallCenterAgentEvents(loginNames)
            .SetTimeout(0)
            .Build();

        //Subscribe (send subsciption events)
        await _o2gApp.SubscribeAsync(s);
        _subscriptionSuccess = true;
        //_logger.LogInformation("Subscription for events successfully done!!!");
      }
      catch (Exception ex)
      {
        _logger.LogError("Subscription for events failed due to this exception: " + ex.Message);
      }
      return _subscriptionSuccess;
    }

    /// <summary>
    /// CallCreated telephony call event handler. Executed when incoming call arrives or user starts dialling.  Can be used to show screen popup
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void TelephonyService_CallCreated(object sender, O2GEventArgs<OnCallCreatedEvent> e)
    {

      CallData _callData = e.Event.CallData;
      string _callRef = e.Event.CallRef;
      string _loginName = e.Event.LoginName;
      string _eventName = e.Event.EventName;

      MediaState mediaState = _callData.State;
      OngoingCallInfo callInfo = new OngoingCallInfo();
      callInfo.CallRef = _callRef;
      callInfo.LoginName = _loginName;
      if (mediaState == MediaState.RingingIncoming)
      {
        _logger.LogInformation("EVENT " + _eventName + " -------------------- e.Event.CallRef: " + _callRef + ", e.Event.LoginName: " + _loginName + ", Call state: RINGING INCOMING");
        if (!ongoingCalls.ContainsKey(_callRef))
        {
          callInfo.CallDiretion = "Incoming";
          ongoingCalls.Add(_callRef, callInfo);
          incomingCallRef = _callRef;
          announcementMade = false;
        }
      }

    }
    /// <summary>
    /// CallModified telephony call event handler. Executed when call state changed, for example called person answers call and conversation starts. 
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void TelephonyService_CallModified(object sender, O2GEventArgs<OnCallModifiedEvent> e)
    {
      string _callRef = e.Event.CallRef;
      string _loginName = e.Event.LoginName;
      string _eventName = e.Event.EventName;

      if (e.Event.CallData != null)
      {
        CallData _callData = e.Event.CallData;
        MediaState mediaState = _callData.State;

        if (mediaState == MediaState.Active)
        {
          // Confersation is established
          _logger.LogInformation("EVENT " + _eventName + " ------------------- e.Event.CallRef: " + _callRef + ", e.Event.LoginName: " + _loginName + ", Call state: CONVERSATION");

          OngoingCallInfo callInfo = new OngoingCallInfo();
          if (!ongoingCalls.ContainsKey(_callRef))
          {
            callInfo.CallRef = _callRef;
            callInfo.CallStart = DateTime.UtcNow;
            callInfo.LoginName = _loginName;
            ongoingCalls.Add(_callRef, callInfo);
          }
          else
          {
            callInfo = ongoingCalls[_callRef];
            callInfo.CallStart = DateTime.UtcNow;
          }
          //Async task to get caller number and push data to CRM
          GetCalls(_callRef, _loginName);
        }
      }
    }


    /// <summary>
    /// CallRemoved telephony call event handler. Executed when call ended. 
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void TelephonyService_CallRemoved(object sender, O2GEventArgs<OnCallRemovedEvent> e)
    {
      string _callRef = e.Event.CallRef;
      string _loginName = e.Event.LoginName;
      string _eventName = e.Event.EventName;
      if (ongoingCalls.ContainsKey(_callRef))
      {
        _logger.LogInformation("EVENT " + _eventName + " -------------------- e.Event.CallRef: " + _callRef + ", e.Event.LoginName: " + _loginName);
        //Remove call from OngoingCalls
        ongoingCalls.Remove((_callRef));
      }
    }


    /// <summary>
    /// It is used to get call participants number 
    /// </summary>
    /// <param name="callRef"></param>
    /// <param name="loginName"></param>
    private async void GetCalls(string callRef, string loginName)
    {
      string _phoneNumber = "";
      calls = await Task.Run(() => this._o2gApp.TelephonyService.GetCallsAsync(loginName));
      Console.WriteLine("Number of calls = " + calls.Count);
      PbxCall pbxCall1 = null;
      PbxCall pbxCall2 = null;
      if (calls.Count == 1)
      {
        pbxCall1 = calls[0];
        List<Participant> participants = pbxCall1.Participants;
        if (participants.Count > 0)
        {
          Participant participant = participants[0];
          o2g.Types.CommonNS.PartyInfo partyInfo = participant.Identity;
          o2g.Types.CommonNS.PartyInfo.Identifier identifier = partyInfo.Id;
          //Participant number
          _phoneNumber = identifier.PhoneNumber;
        }
      }
      if (calls.Count == 2)
      {
        pbxCall2 = calls[1];
      }

      if (ongoingCalls.ContainsKey(callRef))
      {
        OngoingCallInfo callInfo = ongoingCalls[callRef];
        if (callInfo.CallDiretion == "Incoming" && _phoneNumber.Length > 3)
        {
          //Phone number correction                    
          _phoneNumber = ReplaceLeadingDigits(_phoneNumber, "000", "+");
          _phoneNumber = ReplaceLeadingDigits(_phoneNumber, "00", "+49");
          PushData(callRef, _phoneNumber, loginName);
        }
      }

    }

    public string ReplaceLeadingDigits(string inputString, string findString, string replaceString)
    {
      string _replaced = inputString;
      if (inputString.StartsWith(findString))
      {
        //we need to cut
        string _cutLeadingDigits = inputString.Substring(findString.Length, inputString.Length - findString.Length);
        _replaced = replaceString + _cutLeadingDigits;
      }
      return _replaced;
    }

    public async Task<bool> PushData(string callId, string callingNumber, string loginName)
    {
      bool _success = false;
      HttpClient httpClient = _httpClientFactory.CreateClient();
      var username = _aPiuser;
      var password = _aPiuserPwd;
      var credentials = $"{username}:{password}";
      var base64Credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes(credentials));
      httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64Credentials);

      string url = _helpDeskUrl;
      try
      {
        // Convert user object to JSON

        JsonData jsonData = new JsonData();
        int i = 0;
        jsonData.CallId = callId;
        jsonData.DestTel = "DEMO Phone";
        jsonData.SourceKey = "TELEPHONE";
        jsonData.SourceTel = callingNumber;
        jsonData.StartedAt = DateTime.Now;
        jsonData.AgentEmail = "";
        AgentCustomfield agentCustomfield = new AgentCustomfield();
        agentCustomfield.Fieldvalue = loginName;
        agentCustomfield.Customfield = "oxe_login";
        jsonData.AgentCustomfield = agentCustomfield;

        string json = JsonConvert.SerializeObject(jsonData, Newtonsoft.Json.Formatting.Indented);

        HttpContent httpContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        HttpResponseMessage response = await httpClient.PostAsync(url, httpContent);
        string content = await response.Content.ReadAsStringAsync();
        //Debug.WriteLine("Submit response content: " + content);
        if (response.StatusCode == System.Net.HttpStatusCode.OK)
        {
          _logger.LogInformation("SUCCESS pushing data");
          _logger.LogInformation("Data sent in JSON format: \n" + "\t" + json);
          _success = true;
        }
        else
        {
          _logger.LogInformation("PushData() response.StatusCode = " + response.StatusCode);
          _logger.LogInformation("Data sent in JSON format: \n" + "\t" + json);
        }
      }
      catch (Exception ex)
      {
        _logger.LogError("ERROR in PushData() " + ex.Message);
      }
      return _success;
    }


  }
}
