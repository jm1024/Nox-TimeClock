using System;
using System.Data;
using System.Configuration;
using System.Web;
using System.Web.Security;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Web.UI.WebControls.WebParts;
using System.Web.UI.HtmlControls;
using System.Text;
using System.IO;
using NoxCore;
using NoxComm;

public partial class _Default : System.Web.UI.Page
{
    // version 0.05.00

    // Server address
    String serverAddress = "127.0.0.1";

    // SMS netCore Account Settings
    public String feedId = "336447"; //"337457";
    public String userName = "9820033441"; //"9820033441";
    public String password = "awtpw"; //"ddwgd"; //
    public String senderId = "Nox";

    //times
    public int arriveBeginHour = 9;
    public int arriveBeginMinute = 30;
    public int arriveEndHour = 11;
    public int arriveEndMinute = 0;
    public int exitBeginHour = 17;
    public int exitBeginMinute = 30;
    public int exitEndHour = 20;
    public int exitEndMinute = 0;

    //recipient and transmission method (smtp or sms) - if blank will be pulled from querystring
    public String recipient = "";
    public String sendVia = "";

    // turn on debug logging
    public Boolean debug = true;

    public String output = "";
    
    ///////////////////////////////////////
    protected void Page_Load(object sender, EventArgs e)
    {
        setup();
    }

    ///////////////////////////////////////
    public void setup()
    {

        String id = getVal("id");
        String zone = getVal("zone");
        String eventType = getVal("eventType");
        String eventDate = getVal("eventDate");

        if (recipient == "")
        {
            recipient = getVal("recipient");
        }
        if (sendVia == "")
        {
            sendVia = getVal("sendVia");
        }

        //get the name of the item
        NoxCore.cId i = new NoxCore.cId();
        String name = i.getName(id);

	    try
	    {
            Boolean bSend = false;
            String verb = "";

            //convert time to ints for comparison
            DateTime dEvent = Convert.ToDateTime(eventDate);
            String resKey = "";

            //arrival
            if(m_inTimeWindow(dEvent, arriveBeginHour, arriveBeginMinute, arriveEndHour, arriveEndMinute))
            {
                verb = "entered";
                resKey = verb + "." + dEvent.Month.ToString() + "." + dEvent.Day.ToString();
                if (m_getLastMovement(id) != resKey)
                {
                    m_setLastMovement(id, resKey);
                    bSend = true; 
                }
            }

            //exit
            if (m_inTimeWindow(dEvent, exitBeginHour, exitBeginMinute, exitEndHour, exitEndMinute))
            {
                verb = "exited";
                resKey = verb + "." + dEvent.Month.ToString() + "." + dEvent.Day.ToString();
                if (m_getLastMovement(id) != resKey)
                {
                    m_setLastMovement(id, resKey);
                    bSend = true;
                }
            }

            //transmission
            if (bSend)
            {
                //format the message
                String message = "";
                message = name + " has " + verb + " " + zone + " at " + dEvent.ToShortTimeString();

                if (sendVia.ToLower() == "smtp")
                {
                    m_sendSMTP(recipient, message);
                }

                if (sendVia.ToLower() == "sms")
                {
                    m_sendSMS(recipient, message);
                }
            }

            if (debug)
            {
                NoxCore.cLog log = new NoxCore.cLog();
                log.write("timeClock.aspx.cs.setup() debug id:" + id + " send:" + bSend.ToString() + " sendVia:" + sendVia + " verb:" + verb + " eventDate:" + dEvent.ToString() + " resKey:" + resKey + " dbKey:" + m_getLastMovement(id));
            }
	    }
	    catch(Exception e)
	    {
		    NoxCore.cLog log = new NoxCore.cLog();
		    log.write("timeClock.aspx.cs.setup() " + e.Message);
	    }
    }

    /////////////////////////////////////////
    public void m_sendSMTP(String recipient, String message)
    {
        NoxComm.cSMTP mail = new NoxComm.cSMTP();
        String body = "";
        String subject = "";

        //standard settings
        mail.server = NoxCore.cConfig.smtpServer;
        mail.port = NoxCore.cConfig.smtpPort;
        mail.username = NoxCore.cConfig.smtpUserName;
        mail.password = NoxCore.cConfig.smtpPassword;
        mail.TLS = NoxCore.cConfig.smtpTLS;
        mail.from = NoxCore.cConfig.smtpFrom;

        mail.to = recipient;
        mail.subject = message;
        mail.body = message;

        String response = mail.send();

        //debug logging
        if (debug)
        {
            NoxCore.cLog log = new NoxCore.cLog();
            log.write("timeClock.aspx.cs.sendSMTP() " + response + " server: " + mail.server);
        }

    }

    /////////////////////////////////////////
    public void m_sendSMS(String recipient, String message)
    {
        NoxComm.cHTTP http = new NoxComm.cHTTP();

        //http://bulkpush.mytoday.com/BulkSms/SingleMsgApi?feedid=1879&username=9967025255&password=hello&To=919967025255&Text=Hellocheck2350&time=200812110950&senderid=testSenderID
        //1) Channel Name : GYAANTECH_API
        //Resource Id : 337457
        //Log in id - 9820033441
        //Password - awtpw
        //NDNC check : ON
        //*URL for checking API:  http://bulkpush.mytoday.com/BulkSms/ 

        try
        {
            // netCore wants spaces replaced with +
            message = message.Replace(" ", "+");

            http.serverName = "bulkpush.mytoday.com";
            http.page = "BulkSms/SingleMsgApi";

            http.addData("feedid", feedId);
            http.addData("username", userName);
            http.addData("password", password);
            http.addData("To", recipient);
            http.addData("Text", message);
            http.addData("senderid", senderId);

            // transmit the request
            output = http.get();
        }
        catch (Exception e)
        {
            NoxCore.cLog log = new NoxCore.cLog();
            log.write("timeClock.aspx.cs.sendSMS(): " + e.Message);
        }

        //debug logging
        if (debug)
        {
            NoxCore.cLog log = new NoxCore.cLog();
            log.write("timeClock.aspx.cs.sendSMS() URL " + http.URL);
            log.write("timeClock.aspx.cs.sendSMS() output " + output);
        }

    }

    /////////////////////////////////////////
    private Boolean m_inTimeWindow(DateTime dEvent, int beginHour, int beginMinute, int endHour, int endMinute)
    {
        int hour = dEvent.Hour;
        int minute = dEvent.Minute;

        Boolean ret = false;

        if(hour == beginHour)
        {
            if (minute >= beginMinute)
            {
                ret = true;
            }
        }

        if (hour == endHour)
        {
            if (minute <= beginMinute)
            {
                ret = true;
            }
        }

        if (hour > beginHour && hour < endHour)
        {
            ret = true;
        }

        //return
        return ret;
    }

    /////////////////////////////////////////
    private String m_getLastMovement(String id)
    {
        NoxCore.cResource res = new NoxCore.cResource();
        res.load(id, "lastMovement");
        return res.value;
    }

    /////////////////////////////////////////
    private void m_setLastMovement(String id, String value)
    {
        NoxCore.cResource res = new NoxCore.cResource();
        res.name = id;
        res.category = "lastMovement";
        res.value = value;
        res.update();
    }

    /////////////////////////////////////////
    // get a value from form or querystring
    public String getVal(String name)
    {
        String ret = "";
        try
        {
            if (Request.Form[name] != null)
            {
                ret = Request.Form[name].ToString();
            }
        }
        catch (Exception)
        { }

        try
        {
            if (ret == "")
            {
                ret = Request.QueryString[name].ToString();
            }
        }
        catch (Exception)
        { }

        return ret;

    }

}