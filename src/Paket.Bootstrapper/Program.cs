﻿using System;
using System.Diagnostics;
using System.IO;
using System.Net;


namespace Paket.Bootstrapper
{
    class Program
    {
        static IWebProxy GetDefaultWebProxy()
        {
            IWebProxy result = WebRequest.GetSystemWebProxy();
            Uri irrelevantDestination = new Uri(@"http://google.com");
            Uri address = result.GetProxy(irrelevantDestination);

            if (address == irrelevantDestination)
                return null;

            return new WebProxy(address) { Credentials = CredentialCache.DefaultCredentials };
        }

        static void Main(string[] args)
        {
            var folder = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            var target = Path.Combine(folder, "paket.exe");
             
            try
            {   var latestVersion = "";
                var ignorePrerelease = true;

                if (args.Length >= 1)
                {
                    if (args[0] == "prerelease")
                    {
                        ignorePrerelease = false;
                        Console.WriteLine("Prerelease requested. Looking for latest prerelease.");
                    }
                    else
                    {
                        latestVersion = args[0];
                        Console.WriteLine("Version {0} requested.", latestVersion);
                    }
                }
                else Console.WriteLine("No version specified. Downloading latest stable.");
                var localVersion = "";

                if (File.Exists(target))
                {
                    try
                    {
                        FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(target);
                        if (fvi.FileVersion != null)
                            localVersion = fvi.FileVersion;
                    }
                    catch (Exception)
                    {
                    }
                }

                var proxy = GetDefaultWebProxy();

                if (latestVersion == "")
                {
                    using (WebClient client = new WebClient())
                    {
                        client.Headers.Add("user-agent", "Paket.Bootstrapper");
                        client.UseDefaultCredentials = true;
                        client.Proxy = proxy;

                        var releasesUrl = "https://api.github.com/repos/fsprojects/Paket/releases";
                        var data = client.DownloadString(releasesUrl);
                        var start = 0;
                        while (latestVersion == "")
                        {
                            start = data.IndexOf("tag_name", start) + 11;
                            var end = data.IndexOf("\"", start);
                            latestVersion = data.Substring(start, end - start);
                            if (latestVersion.Contains("-") && ignorePrerelease)
                                latestVersion = "";
                        }
                    }
                }

                if (!localVersion.StartsWith(latestVersion))
                {
                    var url = String.Format("https://github.com/fsprojects/Paket/releases/download/{0}/paket.exe", latestVersion);

                    Console.WriteLine("Starting download from {0}", url);

                    var request = (HttpWebRequest)HttpWebRequest.Create(url);

                    request.UseDefaultCredentials = true;
                    request.Proxy = proxy;

                    request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                    using (HttpWebResponse httpResponse = (HttpWebResponse)request.GetResponse())
                    {
                        using (Stream httpResponseStream = httpResponse.GetResponseStream())
                        {
                            const int bufferSize = 4096;
                            byte[] buffer = new byte[bufferSize];
                            int bytesRead = 0;

                            using (FileStream fileStream = File.Create(target))
                            {
                                while ((bytesRead = httpResponseStream.Read(buffer, 0, bufferSize)) != 0)
                                {
                                    fileStream.Write(buffer, 0, bytesRead);
                                }
                            }
                        }
                    }
                }
                else
                {
                    Console.WriteLine("Paket.exe {0} is up to date.", localVersion);
                }

            }
            catch (Exception exn)
            {
                if (!File.Exists(target))
                    Environment.ExitCode = 1;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(exn.Message);
            }
        }
    }
}