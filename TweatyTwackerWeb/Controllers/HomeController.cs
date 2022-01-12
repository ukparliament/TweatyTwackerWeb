using TweatyTwackerWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.ServiceModel;
using System.ServiceModel.Syndication;
using System.Xml;
using System.Text;
using System.IO;

namespace TweatyTwackerWeb.Controllers
{

    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        IConfiguration _configuration;

        public HomeController(ILogger<HomeController> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        [Route("/")]
        [HttpGet]
        public IActionResult Index()
        {
            var treaties = GetInstruments();
            return View(treaties);
        }

        [Route("/Instrument/{id}")]
        [HttpGet]
        public IActionResult Instrument(string id)
        {
            Treaty treaty = GetInstrument(id);
            return View(treaty);
        }

        [ResponseCache(Duration = 1200)]
        [HttpGet]
        [Route("/Rss")]
        public IActionResult Rss()
        {
            var feed = new SISyndicationFeed();

            feed.Copyright = new TextSyndicationContent("https://www.parliament.uk/site-information/copyright-parliament/open-parliament-licence/");
            feed.Title = new TextSyndicationContent("Treaties laid before the UK Parliament");
            feed.Description = new TextSyndicationContent("Updates whenever a treaty is laid before a House in the UK Parliament");
            feed.Language = "en-uk";

            XmlDocument doc = new XmlDocument();
            XmlElement feedElement = doc.CreateElement("link");
            feedElement.InnerText = "https://api.parliament.uk/tweatytwacker";
            feed.ElementExtensions.Add(feedElement);

            XmlElement feedElement1 = doc.CreateElement("managingEditor");
            feedElement1.InnerText = "somervillea@parliament.uk (Anya Somerville)";
            feed.ElementExtensions.Add(feedElement1);

            XmlElement feedElement2 = doc.CreateElement("pubDate");
            feedElement2.InnerText = DateTime.Now.ToString();
            feed.ElementExtensions.Add(feedElement2);

            var items = new List<SyndicationItem>();
            var treaties = GetInstruments();
            foreach (var treaty in treaties.Where(x=>x.IsTweeted))
            {
                var item = new SyndicationItem();
                item.Title = new TextSyndicationContent(treaty.Name);
                item.Content = new TextSyndicationContent(treaty.Description);
                item.PublishDate = treaty.LaidDate;
                XmlElement feedElement3 = doc.CreateElement("guid");
                feedElement3.InnerText = treaty.Url;
                item.ElementExtensions.Add(feedElement3);

                XmlElement feedElement4 = doc.CreateElement("link");
                feedElement4.InnerText = treaty.Url;
                item.ElementExtensions.Add(feedElement4);
                items.Add(item);
            }

            feed.Items = items;
            var settings = new XmlWriterSettings
            {
                Encoding = Encoding.UTF8,
                NewLineHandling = NewLineHandling.Entitize,
                NewLineOnAttributes = true,
                Indent = true
            };
            using (var stream = new MemoryStream())
            {
                using (var xmlWriter = XmlWriter.Create(stream, settings))
                {
                    var rssFormatter = new Rss20FeedFormatter(feed, false);
                    rssFormatter.WriteTo(xmlWriter);
                    xmlWriter.Flush();
                }
                return File(stream.ToArray(), "application/rss+xml; charset=utf-8");
            }
        }

        Treaty GetInstrument(string id1)
        {
            var id = "https://id.parliament.uk/" + id1;
            string connectionString = _configuration["TweatyTwackerSqlServer"];

            SqlConnection connection = new SqlConnection(connectionString);
            connection.Open();

            using (SqlCommand cmd = new SqlCommand("Read from database", connection))
            {
                String sql = $@"SELECT 
	                                  [TreatyName]
                                          ,[LeadOrg]
                                          ,[Series]
                                          ,[LaidDate]
                                          ,[TreatyUri]
                                          ,[WorkPackageUri]
                                          ,[TnaUri]
                                          ,[IsTweeted]
                                      FROM [dbo].[TweatyTwackerTreaty]
                                WHERE [TreatyUri] = '{id}'";

                using (SqlCommand command = new SqlCommand(sql, connection))
                {
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var treaty = new Treaty();
                            treaty.Name = reader.GetString(0);
                            treaty.LeadOrganisation = reader.GetString(1);
                            treaty.Series = reader.GetString(2);
                            treaty.LaidDate = reader.GetDateTimeOffset(3);
                            treaty.Id = reader.GetString(4);
                            treaty.WorkPackageId = reader.GetString(5);
                            treaty.Link = reader.GetString(6);
                            treaty.IsTweeted = reader.GetBoolean(7);
                            connection.Close();
                            return treaty;
                        }
                    }
                }
            }

            connection.Close();
            return null;
        }

        IEnumerable<Treaty> GetInstruments()
        {
            string connectionString = _configuration["TweatyTwackerSqlServer"];

            SqlConnection connection = new SqlConnection(connectionString);
            connection.Open();

            List<Treaty> treaties = new List<Treaty>();
            using (SqlCommand cmd = new SqlCommand("Read from database", connection))
            {
                String sql = @"SELECT 
	                                   [TreatyName]
                                          ,[LeadOrg]
                                          ,[Series]
                                          ,[LaidDate]
                                          ,[TreatyUri]
                                          ,[WorkPackageUri]
                                          ,[TnaUri]
                                          ,[IsTweeted]
                                      FROM [dbo].[TweatyTwackerTreaty]";

                using (SqlCommand command = new SqlCommand(sql, connection))
                {
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var treaty = new Treaty();
                            treaty.Name = reader.GetString(0);
                            treaty.LeadOrganisation = reader.GetString(1);
                            treaty.Series = reader.GetString(2);
                            treaty.LaidDate = reader.GetDateTimeOffset(3);
                            treaty.Id = reader.GetString(4);
                            treaty.WorkPackageId = reader.GetString(5);
                            treaty.Link = reader.GetString(6);
                            treaty.IsTweeted = reader.GetBoolean(7);
                            treaties.Add(treaty);
                        }
                    }
                }
            }

            connection.Close();
            return treaties.OrderByDescending(x => x.LaidDate);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
