using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;
using System.Threading.Tasks;
using System;
using TMPro;
using System.Net.Http;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.IO;
using UnityEngine.Android;

public class MainManager : MonoBehaviour
{
    public bool tagFound = false;
    public TMP_Text tag_output_text;

    HttpClient client = null;
    
    string keyFile = "/storage/emulated/0/.TECConfig/Config.txt";
    string authKey;

    // Start is called before the first frame update
    void Start()
    {
        string[] config = File.ReadAllLines(keyFile);
        authKey = config[0];
        client = new HttpClient { BaseAddress = new Uri($"http://{config[1]}:6969") };
    }

    private async void Update()
    {
        if (!tagFound)
        {
            tag_output_text.text = "Scan Badge\nIn/Out\n";
            string tagData = DetectNFCTag();
            if (tagData == "NOTAG") { return; }
            tagFound = true;

            string attendeeString = await client.GetStringAsync($"/GetAttendee?{tagData.Split("&")[1]}");
            if (attendeeString == "[null]" ) { tag_output_text.text = "Badge Not Registered!"; await Task.Delay(2000); tagFound = false; return; }
            Attendee attendee = JsonConvert.DeserializeObject<Attendee[]>( attendeeString )[0];

            // check if name on badge matches server
            // if not, invalid badge!
            if ($"Name={attendee.Name}" != tagData.Split("&")[0] ) { tag_output_text.text = "Badge Invalid!"; await Task.Delay(2000); tagFound = false; return; }
            
            // make request to scan in/out
            tag_output_text.text = $"Scanning {(attendee.AtCon ? "Out" : "In")} {attendee.Name}";
            string result = await client.PostAsync($"/Attendee{(attendee.AtCon ? "Left" : "Join")}?ID={attendee.ID}", 
                new MultipartFormDataContent{ 
                    new StringContent(authKey) { Headers = {
                        ContentDisposition = 
                            new ContentDispositionHeaderValue("form-data") { Name = "Auth"} } } })
                                .Result.Content.ReadAsStringAsync();

            await ProcessResult(result);
        }
    }

    async Task ProcessResult(string result)
    {
        switch (result)
        {
            case "SUCCESS":
                tag_output_text.text = $"OK!";
                await Task.Delay(2000);
                tagFound = false;
                return;
            case "NOID":
                tag_output_text.text = $"No ID found!";
                await Task.Delay(2000);
                tagFound = false;
                return;
            case "AuthError":
                tag_output_text.text = $"Server Auth Error!";
                return;
            case "NOTFOUND":
                tag_output_text.text = $"Attendee Not Found!";
                await Task.Delay(2000);
                tagFound = false;
                return;
            default:
                tag_output_text.text = $"Unknown Error";
                await Task.Delay(2000);
                tagFound = false;
                return;
        }
    }

    string DetectNFCTag()
    {
        try
        {
            // Create new NFC Android object
            AndroidJavaObject _mActivity = new AndroidJavaClass("com.unity3d.player.UnityPlayer").GetStatic<AndroidJavaObject>("currentActivity");
            AndroidJavaObject _mIntent = _mActivity.Call<AndroidJavaObject>("getIntent");
            string _sAction = _mIntent.Call<String>("getAction");

            if (_sAction == "android.nfc.action.NDEF_DISCOVERED")
            {
                AndroidJavaObject[] rawMsg = _mIntent.Call<AndroidJavaObject[]>("getParcelableArrayExtra", "android.nfc.extra.NDEF_MESSAGES");
                AndroidJavaObject[] records = rawMsg[0].Call<AndroidJavaObject[]>("getRecords");
                byte[] payLoad = records[0].Call<byte[]>("getPayload");
                string result = System.Text.Encoding.Default.GetString(payLoad);
                _mIntent.Call("removeExtra", "android.nfc.extra.NDEF");
                _mIntent.Call<AndroidJavaObject>("setAction", "");

                result = result.Remove(0, 12); // removes custom uri attendee://, to return only attendee info
                return result;
            }
            return "NOTAG";
        }
        catch (Exception ex)
        {
            return ex.Message;
        }

    }
}

public class Attendee
{
    public Attendee(string name, int iD, bool atCon, List<DateTime> joinDates, List<DateTime> leaveDates)
    {
        Name = name;
        ID = iD;
        AtCon = atCon;
        JoinDates = joinDates;
        LeaveDates = leaveDates;
    }

    public string Name { get; set; }
    public int ID { get; set; }
    public bool AtCon { get; set; }
    public List<DateTime> JoinDates { get; set; }
    public List<DateTime> LeaveDates { get; set; }
}