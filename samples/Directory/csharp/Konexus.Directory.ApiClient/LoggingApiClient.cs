using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text;

namespace Konexus.Directory.ApiClient.Client
{
    public partial class ApiClient
    {
        partial void InterceptRequest(HttpRequestMessage req)
        {
            Serilog.Log.Debug($"{req.Method} - {req.RequestUri}");

            string body = null;
            if(req.Content != null)
            {
                body = req.Content.ReadAsStringAsync().Result;
                Serilog.Log.Debug($"\tBody: {body}");
            }
        }
        partial void InterceptResponse(HttpRequestMessage req, HttpResponseMessage response)
        {
            Serilog.Log.Debug($"Response: {(int)response.StatusCode} - {response.ReasonPhrase} - {response.Headers?.Location}");

            string body = null;
            if (response.Content != null)
            {
                body = response.Content.ReadAsStringAsync().Result;
                Serilog.Log.Debug($"\tBody: {body}");
            }
        }
    }
}
