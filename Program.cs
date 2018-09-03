/*
 * Reageert meteen op een huurobject wanneer er een woning met de gekozen filters beschikbaar is (mooi voor 1e reageerder)
 * NOTE: crashed vaak, werkt niet (volledig)
 * Code is voor educationele doeleinden
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Globalization;
using System.Threading;

namespace WoonezieHouseApplicatorCS
{
    class Program
    {
        static StreamWriter logfile;
        static SortedSet<int> reacted_to_houses;

        static void Log(string message)
        {
            string to_write = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + ": " + message;
            Console.WriteLine(to_write);
            logfile.WriteLine(to_write);
        }

        static void LogConsole(string message)
        {
            string to_write = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + ": " + message;
            Console.WriteLine(to_write);
        }

        static bool IsGreater(string date)
        {
            try
            {
                DateTime now = DateTime.Now;

                DateTime dt_date;
                if(!DateTime.TryParseExact(date, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture,
                          DateTimeStyles.None, out dt_date))
                {
                    return false;
                }

                return now < dt_date;
            }
            catch(Exception)
            {
                return false;
            }
        }

        static void ApplyForHouses()
        {
            System.Net.CookieContainer _cookieContainer = new System.Net.CookieContainer();
            HttpClientHandler handler = new HttpClientHandler
            {
                AllowAutoRedirect = true,
                UseCookies = true
            };
            HttpClient client = new HttpClient(handler);

            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; WOW64; rv:55.0) Gecko/20100101 Firefox/55.0");
            client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.5");

            var response = client.GetAsync("https://www.wooniezie.nl/").Result;

            client.DefaultRequestHeaders.Add("Referer", "https://www.wooniezie.nl/");

            response = client.GetAsync("https://www.wooniezie.nl/mijn-wooniezie/inloggen/").Result;

            var values = new Dictionary<string, string>
            {
                { "__id__", "Account_Form_LoginFrontend" },
                { "username", "" },
                { "password", "" },
                { "submit", "inloggen" }
            };

            client.DefaultRequestHeaders.Remove("Referer");
            client.DefaultRequestHeaders.Add("Referer", "https://www.wooniezie.nl/mijn-wooniezie/inloggen/");

            var content = new FormUrlEncodedContent(values);
            response = client.PostAsync("https://www.wooniezie.nl/mijn-wooniezie/inloggen/", content).Result;

            var responseString = client.GetAsync("https://www.wooniezie.nl/portal/account/frontend/getaccount/format/json").Result.Content.ReadAsStringAsync().Result;
            JToken account = null;
            try
            {
                account = JObject.Parse(responseString)["account"];
            }
            catch (Exception e)
            {
                throw new Exception("Login parsing failed, response: \n>>>BEGIN<<<\n" + responseString + "\n>>>END<<<\n\n inner exception message:\n" + e.Message);
            }
            if (!account.HasValues || account["username"].ToString() != values["username"])
            {
                throw new Exception("ERROR: Cannot login, response: " + responseString);
            }

            // &ik-zoek-een=4&ik-zoek-een=1&ik-zoek-een=2
            SortedSet<int> allowed_dwelling_type = new SortedSet<int>(new int[] 
            {
                2, // Appartement met lift
                // 3, // Appartament zonder lift
                4, // Eengezinswoning
                6 // Seniorenwoning
                //10, // Parkeerplaats
                //14, // Studio
            });

            // &locatie=Brouwhuis-Helmond&locatie=Dierdonk-Helmond&locatie=Helmond-Noord-Helmond&locatie=Helmond-Oost-Helmond&locatie=Helmond-West-Helmond
            // &locatie=Mierlo-Hout-Helmond&locatie=Rijpelberg-Helmond&locatie=Stiphout-Helmond&locatie=Warande-Helmond&locatie=Kersenwijk-Mierlo
            // &locatie=Lijndje e.o.-Mierlo&locatie=Luchen-Mierlo&locatie=Mierlo-Centrum-Mierlo&locatie=Mierlo-De Loo-Mierlo&locatie=Neerakker I-Mierlo
            // &locatie=Neerakker II-Mierlo
            SortedSet<string> allowed_quarter_city = new SortedSet<string>(new string[]
            {
                "Brouwhuis-Helmond",
                "Dierdonk-Helmond",
                "Helmond-Noord-Helmond",
                "Helmond-Oost-Helmond",
                "Helmond-West-Helmond",
                "Mierlo-Hout-Helmond",
                "Rijpelberg-Helmond",
                "Stiphout-Helmond",
                "Warande-Helmond",
                "Kersenwijk-Mierlo",
                "Lijndje e.o.-Mierlo",
                "Luchen-Mierlo",
                "Mierlo-Centrum-Mierlo",
                "Mierlo-De Loo-Mierlo",
                "Neerakker I-Mierlo",
                "Neerakker II-Mierlo"
            });

            // &huurprijs=0&huurprijs=628.76
            double max_price = 628.76;

            // &aantal-slaapkamers=2&aantal-slaapkamers=3
            int min_sleeping_rooms = 2;

            // &woningtype=1&woningtype=4&woningtype=5&woningtype=7&woningtype=12&woningtype=13
            // ???

            // &toekenning=3(eerste reactie)&toekenning=1&toekenning=2
            // don't care

            //int checks = 0;
            //int house_count = 0;
            //int login_checks = 0;
            while (true)
            {
                logfile.Flush();
                Thread.Sleep(1000);

                client.DefaultRequestHeaders.Remove("Referer");
                client.DefaultRequestHeaders.Add("Referer", "https://www.wooniezie.nl/aanbod/te-huur");

                responseString = client.GetAsync("https://www.wooniezie.nl/portal/object/frontend/getallobjects/format/json").Result.Content.ReadAsStringAsync().Result;

                JToken houses = null;
                try
                {
                    houses = JObject.Parse(responseString)["result"];
                }
                catch(Exception e)
                {
                    throw new Exception("Houses parsing failed, response: \n>>>BEGIN<<<\n" + responseString + "\n>>>END<<<\n\n inner exception message:\n" + e.Message);
                }

                Dictionary<int, int> houses_to_add = new Dictionary<int, int>();
                
                foreach (var house in houses)
                {
                    //++house_count;
                    int dwellingType_id = int.Parse((string)house["dwellingType"]["id"]);
                    if (allowed_dwelling_type.Contains(dwellingType_id))
                    {
                        if (
                            Double.Parse((string)house["totalRent"]) <= max_price &&
                            int.Parse((string)house["sleepingRoom"]["amountOfRooms"]) >= min_sleeping_rooms &&
                            (string)house["rentBuy"] == "Huur" &&
                                (allowed_quarter_city.Contains(house["quarter"]["name"].ToString() + "-" + house["city"]["name"].ToString()) ||
                                allowed_quarter_city.Contains(house["quarter"]["name"].ToString() + "-" + house["municipality"]["name"].ToString())) &&
                                IsGreater((string)house["closingDate"]) &&
                                int.Parse((string)house["model"]["modelCategorie"]["id"]) == 3
                            )
                        {
                            int id = int.Parse((string)house["id"]);
                            int assignmentID = int.Parse((string)house["assignmentID"]);
                            if (!reacted_to_houses.Contains(id))
                            {
                                Log("Adding house ID/assignmentID: " + id + " / " + assignmentID);
                                houses_to_add.Add(id, assignmentID);
                            }
                        }
                    }
                }

                foreach (KeyValuePair<int, int> house in houses_to_add)
                {
                    client.DefaultRequestHeaders.Remove("Referer");
                    client.DefaultRequestHeaders.Add("Referer", "https://www.wooniezie.nl/aanbod/te-huur/details/?dwellingID=" + house.Key.ToString());


                    responseString = client.GetAsync("https://www.wooniezie.nl/portal/object/frontend/getobject/format/json?id=" + house.Key.ToString()).Result.Content.ReadAsStringAsync().Result;

                    try
                    {
                        JToken house_data = null;
                        try
                        {
                            house_data = JObject.Parse(responseString)["result"];
                        }
                        catch (Exception e)
                        {
                            throw new Exception("House data parsing failed, response: \n>>>BEGIN<<<\n" + responseString + "\n>>>END<<<\n\n inner exception message:\n" + e.Message);
                        }

                        bool logged_in = (bool)house_data["reactionData"]["loggedin"];
                        bool can_react = (bool)house_data["reactionData"]["kanReageren"];

                        if (!logged_in)
                        {
                            throw new Exception("Logged out");
                        }
                        else if ((string)house_data["reactionData"]["action"] == "remove")
                        {
                            reacted_to_houses.Add(house.Key);
                        }
                        else if (can_react)
                        {
                            Log("Reacting to house ID: " + house.Key.ToString());
                            responseString = client.GetAsync("https://www.wooniezie.nl/aanbod/te-huur/details/?do=react&add=" + house.Value.ToString() + "&redirect=1&dwellingID=" + house.Key.ToString()).Result.Content.ReadAsStringAsync().Result;
                        }
                    }
                    catch(Exception e)
                    {
                        throw new Exception("Reacting to house " + house.Key.ToString() + " failed with exception: " + e.ToString());
                    }
                }

                /*if (++checks == 86400)
                {
                    LogConsole("Checked " + house_count.ToString() + " house entries (" + (int)(house_count / checks) + " per loop)");
                    checks = 0;
                    house_count = 0;
                }*/

                /*if(++login_checks == 600)
                {
                    login_checks = 0;
                    responseString = client.GetAsync("https://www.wooniezie.nl/portal/account/frontend/getaccount/format/json").Result.Content.ReadAsStringAsync().Result;
                    account = JObject.Parse(responseString)["account"];
                    if (!account.HasValues || account["username"].ToString() != values["username"])
                    {
                        client.DefaultRequestHeaders.Remove("Referer");
                        client.DefaultRequestHeaders.Add("Referer", "https://www.wooniezie.nl/");

                        response = client.GetAsync("https://www.wooniezie.nl/mijn-wooniezie/inloggen/").Result;

                        client.DefaultRequestHeaders.Remove("Referer");
                        client.DefaultRequestHeaders.Add("Referer", "https://www.wooniezie.nl/mijn-wooniezie/inloggen/");

                        response = client.PostAsync("https://www.wooniezie.nl/mijn-wooniezie/inloggen/", content).Result;

                        responseString = client.GetAsync("https://www.wooniezie.nl/portal/account/frontend/getaccount/format/json").Result.Content.ReadAsStringAsync().Result;
                        account = JObject.Parse(responseString)["account"];
                        if (!account.HasValues || account["username"].ToString() != values["username"])
                        {
                            throw new Exception("ERROR: Cannot refresh login");
                        }
                        else
                        {
                            Log("Login refresh successful");
                        }
                    }
                }*/
            }
        }

        static void Main(string[] args)
        {
            while (true)
            {
                reacted_to_houses = new SortedSet<int>();
                try
                {
                    using (logfile = File.AppendText("WooniezieHouseApplicatorCS.log"))
                    {
                        Log("Running House Applicator...");
                        try
                        {
                            ApplyForHouses();
                        }
                        catch (Exception e)
                        {
                            Log("Main Error: " + e.ToString());
                        }
                        Log("... House Applicattor stopped, Re-running...");
                        logfile.Flush();
                    }
                }
                catch (Exception e)
                {
                    LogConsole("Logger died: " + e.ToString());
                }
                Thread.Sleep(1000);
            }
        }
    }
}
