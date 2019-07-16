/*
This program gets data from multiple sources via APIs, parses the data,
and sends it to Azure Log Analytics in a well formated state.

This project was put on hold, however the existing prototype below can get data from an API and send it to a table in Log Analytcs successfully

*/

// Import dependencies, these are loaded into the Azure Functions environment via the function.proj file
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

//Azure function timer trigger, also functions as "main"
public static void Run(TimerInfo myTimer, ILogger log)
{
    log.LogInformation($"C# CTI API function executed at: {DateTime.Now}"); // Function logging

    // Data sources:
    DataSource src1 = new DataSource("time API", "http://worldtimeapi.org/api/timezone/America/Argentina/Salta", 1, "", "1", "423");
    //DataSource src2 = new DataSource("time API", "http://data.phishtank.com/data/online-valid.csv", 1, "", "4);
    //DataSource src3 = new DataSource("AlienVault","https://otx.alienvault.com/api/v1/indicators/export?modified_since=2019-07-09", 1, "<api key>", "1");

    // Calls to methods for testing, currently gets data from the time API and inputs it into the <your table> table on Log Analytics
    var returnedData = src1.getData();
    log.LogInformation($"C# returnedData=: {returnedData}");
    returnedData = src1.sendData(returnedData);
    log.LogInformation($"C# returnedData=: {returnedData}");
    //var returnedData2 = src2.getData();
    //log.LogInformation($"C# returnedData2=: {returnedData2}");
    //var returnedData3 =src3.getData();
    //log.LogInformation($"C# returnedData3=: {returnedData3}");
}

//Class declaration for dataSource class
public class DataSource
{
    //Instance variables
    public String sourceName;
    public String URI;
    public int dataType;
    public String APIKey;
    public String parseFormat; 
    public String parseDataSeq; // Future use for telling program the sequence to parse data. For example, some API's output a csv that must be converted to JSON and then parsed further

    // Constructor for DataSource
    public DataSource(String sourceName, String URI, int dataType, String APIKey, String parseFormat, String parseDataSeq)
    {
        this.sourceName = sourceName;
        this.URI = URI;
        this.dataType = dataType;
        this.APIKey = APIKey;
        this.parseFormat = parseFormat;
        this.parseDataSeq = parseDataSeq;
    }

    // Methods:
        public String getData() // Get data from a provided API source
        {
            HttpClient client = new HttpClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", APIKey);
                    var response = client.GetAsync(URI).Result;

                    if (response.IsSuccessStatusCode)
                    {
                        var responseContent = response.Content; 

                        // by calling .Result you are synchronously reading the result
                        string responseString = responseContent.ReadAsStringAsync().Result;
                        String responseData = responseString;
                        return responseData;
                        //Console.WriteLine(responseString);
                    }
            var defaultResponse = "The API did not provide a response";
            return defaultResponse;
    
        }
        public String parseData(String responseData) // Parse the raw output of any API source into well formatted JSON for the correct date range
        {
            // No parsing has yet been configured
            return responseData;
        }
        public String sendData(String returnedData)  // Sends parsed data to the correct table in our Log Analytics instance
        {
            // Build authorization signature for API POST. Details of this are here: https://docs.microsoft.com/en-us/azure/azure-monitor/platform/data-collector-api
            var customerID = "<customer/appID>";
            var sharedKey = "<Log Analytics Shared Key>"; 
            var logName = "<Log Analytics Table Name>"; // Name of the table in Log Analytics
            var datestring = DateTime.UtcNow.ToString("r");
            var jsonBytes = Encoding.UTF8.GetBytes(returnedData);
            string stringToHash = "POST\n" + jsonBytes.Length + "\napplication/json\n" + "x-ms-date:" + datestring + "\n/api/logs";
            
    		var encoding = new System.Text.ASCIIEncoding();
			byte[] keyByte = Convert.FromBase64String(sharedKey);
			byte[] messageBytes = encoding.GetBytes(stringToHash);
			var hmacsha256 = new HMACSHA256(keyByte);
			
			byte[] hash = hmacsha256.ComputeHash(messageBytes);
		    String hashedString = Convert.ToBase64String(hash);
            string signature = "SharedKey " + customerID + ":" + hashedString;
			
            // Send POST call containing our data to the Log Analytics API endpoint
			try
			{
				string url = "https://" + customerID + ".ods.opinsights.azure.com/api/logs?api-version=2016-04-01";

				System.Net.Http.HttpClient client = new System.Net.Http.HttpClient();
				client.DefaultRequestHeaders.Add("Accept", "application/json");
				client.DefaultRequestHeaders.Add("Log-Type", logName);
				client.DefaultRequestHeaders.Add("Authorization", signature);
				client.DefaultRequestHeaders.Add("x-ms-date", datestring);
				client.DefaultRequestHeaders.Add("time-generated-field", datestring);

				System.Net.Http.HttpContent httpContent = new StringContent(returnedData, Encoding.UTF8);
				httpContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
				Task<System.Net.Http.HttpResponseMessage> response = client.PostAsync(new Uri(url), httpContent);

				System.Net.Http.HttpContent responseContent = response.Result.Content;
				string result = responseContent.ReadAsStringAsync().Result;
                returnedData = result;
			}
			catch (Exception excep) // Catch bad repsonses
			{
                returnedData = excep.Message;
			}
            return returnedData;
        }
}