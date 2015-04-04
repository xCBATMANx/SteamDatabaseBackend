﻿/*
 * Copyright (c) 2013-2015, SteamDB. All rights reserved.
 * Use of this source code is governed by a BSD-style license that can be
 * found in the LICENSE file.
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Xml;
using Timer = System.Timers.Timer;
using Dapper;

namespace SteamDatabaseBackend
{
    class RSS
    {
        private class GenericFeedItem
        {
            public string Title { get; set; }
            public string Link { get; set; }
        }

        public Timer Timer { get; private set; }

        private Dictionary<string, byte> Items;

        public RSS()
        {
            Items = new Dictionary<string, byte>();

            Timer = new Timer();
            Timer.Elapsed += Tick;
            Timer.Interval = TimeSpan.FromSeconds(60).TotalMilliseconds;
            //Timer.Start();
        }

        private void Tick(object sender, System.Timers.ElapsedEventArgs e)
        {
            Parallel.ForEach(Settings.Current.RssFeeds, feed =>
            {
                string feedTitle;
                var rssItems = LoadRSS(feed, out feedTitle);

                if (rssItems == null)
                {
                    return;
                }

               /*using (var db = Database.GetConnection())
                {
                    Items = db.Query<RssItem>("SELECT `ID` FROM `RSS` WHERE `Link` IN @Ids", new { Ids = rssItems.Select(x => x.Link) });
                }*/

                var newItems = rssItems.Where(item => !Items.ContainsKey(item.Link));

                foreach (var item in newItems)
                {
                    // Worst hacks EU
                    if (item.Title != "Team Fortress 2 Update Released" && feedTitle != "Steam RSS News Feed")
                    {
                        IRC.Instance.SendMain("{0}{1}{2}: {3} -{4} {5}", Colors.BLUE, feedTitle, Colors.NORMAL, item.Title.Trim(), Colors.DARKBLUE, item.Link);
                    }

                    lock (Items)
                    {
                        Items.Add(item.Link, (byte)1);
                    }
                }
            });
        }

        private static List<GenericFeedItem> LoadRSS(Uri url, out string feedTitle)
        {
            try
            {
                var webReq = WebRequest.Create(url) as HttpWebRequest;
                webReq.UserAgent = "RSS2IRC";
                webReq.Timeout = (int)TimeSpan.FromSeconds(5).TotalMilliseconds;
                webReq.ReadWriteTimeout = (int)TimeSpan.FromSeconds(5).TotalMilliseconds;

                using (var response = webReq.GetResponse())
                {
                    using (var reader = new XmlTextReader(response.GetResponseStream()))
                    {
                        return ReadFeedItems(reader, out feedTitle);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.WriteError("RSS", "Unable to load RSS feed {0}: {1}", url, ex.Message);

                feedTitle = null;

                return null;
            }
        }

        // http://www.nullskull.com/a/1177/everything-rss--atom-feed-parser.aspx
        private static List<GenericFeedItem> ReadFeedItems(XmlTextReader reader, out string feedTitle)
        {
            feedTitle = string.Empty;

            var itemList = new List<GenericFeedItem>();
            GenericFeedItem currentItem = null;

            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element)
                {
                    string name = reader.Name.ToLowerInvariant();

                    if (name == "item" || name == "entry")
                    {
                        if (currentItem != null)
                        {
                            itemList.Add(currentItem);
                        }

                        currentItem = new GenericFeedItem();
                    }
                    else if (currentItem != null)
                    {
                        reader.Read();

                        switch (name)
                        {
                            case "title":
                                currentItem.Title = reader.Value;
                                break;
                            case "link":
                                currentItem.Link = reader.Value;
                                break;
                        }
                    }
                    else if (name == "title")
                    {
                        reader.Read();

                        feedTitle = reader.Value;
                    }
                }
            }

            return itemList;
        }
    }
}