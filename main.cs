using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using TShockAPI;
using Terraria;
using Newtonsoft.Json;
using TerrariaApi.Server;
using Mono.Data.Sqlite;
using MySql.Data.MySqlClient;
using TShockAPI.DB;
using System.Data;
using Terraria.Localization;
using Microsoft.Xna.Framework;
using OTAPI;


namespace Messenger
{
    [ApiVersion(2, 1)]
    public class Messenger : TerrariaPlugin
    {
        public static IDbConnection MessengerDB;
        public override Version Version
        {
            get { return new Version("1.0.0.0"); }
        }
        public override string Name
        {
            get { return "Messenger"; }
        }
        public override string Author
        {
            get { return "BMS"; }
        }
        public override string Description
        {
            get { return "Messenger Plugin for Terraria Expert Miniserver."; }
        }
        public Messenger(Main game) : base(game)
        {
            Order = 1;
        }
        public override void Initialize()
        {

            ServerApi.Hooks.NetGreetPlayer.Register(this, OnGreetPlayer);

            TShockAPI.Commands.ChatCommands.Add(new Command("msg.use", msg, "msg"));

            switch (TShock.Config.StorageType.ToLower())
            {
                case "mysql":
                    string[] host = TShock.Config.MySqlHost.Split(':');
                    MessengerDB = new MySqlConnection()
                    {
                        ConnectionString = string.Format("Server={0}; Port={1}; Database={2}; Uid={3}; Pwd={4};",
                            host[0],
                            host.Length == 1 ? "3306" : host[1],
                            TShock.Config.MySqlDbName,
                            TShock.Config.MySqlUsername,
                            TShock.Config.MySqlPassword)
                    };
                    break;
                case "sqlite":
                    string sql = Path.Combine(TShock.SavePath, "Messenger.sqlite");
                    MessengerDB = new SqliteConnection(string.Format("uri=file://{0},Version=3", sql));
                    break;
            }
            SqlTableCreator sqlcreator = new SqlTableCreator(MessengerDB,
                MessengerDB.GetSqlType() == SqlType.Sqlite ? (IQueryBuilder)new SqliteQueryCreator() : new MysqlQueryCreator());

            sqlcreator.EnsureTableStructure(new SqlTable("Messages",
                new SqlColumn("ID", MySqlDbType.Int32) { Unique = true, Primary = true, AutoIncrement = true },
                new SqlColumn("Time", MySqlDbType.String, 200),
                new SqlColumn("Sender", MySqlDbType.String, 200),
                new SqlColumn("Receiver", MySqlDbType.String, 200),
                new SqlColumn("WorldID", MySqlDbType.Int32),
                new SqlColumn("Message", MySqlDbType.String, 2000),
                new SqlColumn("Status", MySqlDbType.String, 200)));




        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {

                ServerApi.Hooks.NetGreetPlayer.Deregister(this, OnGreetPlayer);

            }
            base.Dispose(disposing);
        }
        public void OnGreetPlayer(GreetPlayerEventArgs e)
        {
            int count = 0;
            string ids = "<";
            TSPlayer ply = TShock.Players[e.Who];

            

            using (var reader = MessengerDB.QueryReader("SELECT * FROM Messages"))
            {

                

                while (reader.Read())
                {
                    if ((reader.Get<string>("Receiver") == ply.Name) && (reader.Get<string>("Status") == "Unread"))
                    {
                        count++;
                        
                        ids = ids + reader.Get<int>("ID")+" | ";

                        
                    }
                }
            }
            
            if (count!= 0)
            {
                ply.SendMessage("[Messenger] You have "+count +" unread message(s). Use /msg "+ids.Remove(ids.Length-3)+">", Color.LightBlue);
            }

        }
        private void msg(CommandArgs args)
        {
            int output;
            if ((args.Parameters.Count < 1))
            {
                args.Player.SendMessage("* Send message to someone: /msg <player> <message>", Color.LightBlue);
                args.Player.SendMessage("* Check your unread messages: /msg unread", Color.LightBlue);
                args.Player.SendMessage("* Check your read messages: /msg read", Color.LightBlue);
                args.Player.SendMessage("* Check your sent messages: /msg sent", Color.LightBlue);
                args.Player.SendMessage("* Check all your inbox: /msg inbox", Color.LightBlue);
                args.Player.SendMessage("* Read a message: /msg <ID>", Color.LightBlue);
                args.Player.SendMessage("* Delete a message: /msg del <ID>", Color.LightBlue);
                return;
            }
            if (args.Parameters[0] == "unread")
            {
                int pageNumber = 1;
                int count = 0;
                if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out pageNumber))
                    return;
                var lines = new List<string> { };
                String total = "";
                using (var reader = MessengerDB.QueryReader("SELECT * FROM Messages"))
                {

                    while (reader.Read())
                    {
                        if ((reader.Get<string>("Receiver") == args.Player.Name) && (reader.Get<string>("Status") == "Unread"))
                        {
                            total = "[ID "+ reader.Get<int>("ID")+"] ["+ reader.Get<string>("Time").Remove(19) + "] " + "* " + reader.Get<string>("Sender") +": "+ reader.Get<string>("Message");
                            count++;
                            lines.Add(total);
                        }
                    }
                }
                PaginationTools.SendPage(args.Player, pageNumber, lines,
                                         new PaginationTools.Settings
                                         {
                                             HeaderFormat = "Unread messages ({0}/{1}):",
                                             FooterFormat = "Type {0}msg unread {{0}} for more messages. Message wil not be marked as read until you use /msg <id>".SFormat(Commands.Specifier),
                                             MaxLinesPerPage = 4
                                         });
                if (count == 0) { args.Player.SendMessage("No messages found.", Color.LightBlue); }
                return;
            }

            if (args.Parameters[0] == "read")
            {
                int pageNumber = 1;
                int count = 0;
                if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out pageNumber))
                    return;
                var lines = new List<string> { };
                String total = "";
                using (var reader = MessengerDB.QueryReader("SELECT * FROM Messages"))
                {

                    while (reader.Read())
                    {
                        if ((reader.Get<string>("Receiver") == args.Player.Name) && (reader.Get<string>("Status") == "Read"))
                        {
                            count++;
                            total = "[ID " + reader.Get<int>("ID") + "] [" + reader.Get<string>("Time").Remove(19) + "] " + "* " + reader.Get<string>("Sender") +  ": " + reader.Get<string>("Message");
                            lines.Add(total);
                        }
                    }
                }
                PaginationTools.SendPage(args.Player, pageNumber, lines,
                                         new PaginationTools.Settings
                                         {
                                             HeaderFormat = "Read messages ({0}/{1}):",
                                             FooterFormat = "Type {0}msg read {{0}} for more messages.".SFormat(Commands.Specifier),
                                             MaxLinesPerPage = 4
                                         });
                if (count == 0) { args.Player.SendMessage("No messages found.", Color.LightBlue); }
                return;
            }

            if (args.Parameters[0] == "sent")
            {
                int pageNumber = 1;
                int count = 0;
                if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out pageNumber))
                    return;
                var lines = new List<string> { };
                String total = "";
                using (var reader = MessengerDB.QueryReader("SELECT * FROM Messages"))
                {

                    while (reader.Read())
                    {
                        if ((reader.Get<string>("Sender") == args.Player.Name))
                        {
                            count++;
                            total = "[ID " + reader.Get<int>("ID") + "] [" + reader.Get<string>("Time").Remove(19) + "] "+"* To: " + reader.Get<string>("Receiver") + ": " + reader.Get<string>("Message");
                            lines.Add(total);
                        }
                    }
                }
                PaginationTools.SendPage(args.Player, pageNumber, lines,
                                         new PaginationTools.Settings
                                         {
                                             HeaderFormat = "Sent messages ({0}/{1}):",
                                             FooterFormat = "Type {0}msg sent {{0}} for more messages.".SFormat(Commands.Specifier),
                                             MaxLinesPerPage = 4
                                         });
                if (count == 0) { args.Player.SendMessage("No messages found.", Color.LightBlue); }
                return;
            }

            if (args.Parameters[0] == "inbox")
            {
                int pageNumber = 1;
                int count = 0;
                if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out pageNumber))
                    return;
                var lines = new List<string> { };
                String total = "";
                using (var reader = MessengerDB.QueryReader("SELECT * FROM Messages"))
                {

                    while (reader.Read())
                    {
                        if ((reader.Get<string>("Receiver") == args.Player.Name) && (reader.Get<string>("Status") != "Deleted"))
                        {
                            count++;
                            total = "[ID " + reader.Get<int>("ID") + "] [" + reader.Get<string>("Time").Remove(19) + "] "+"* " + reader.Get<string>("Sender") +": " + reader.Get<string>("Message");
                            lines.Add(total);
                        }
                    }
                }
                PaginationTools.SendPage(args.Player, pageNumber, lines,
                                         new PaginationTools.Settings
                                         {
                                             HeaderFormat = "Inbox ({0}/{1}):",
                                             FooterFormat = "Type {0}msg inbox {{0}} for more messages.".SFormat(Commands.Specifier),
                                             MaxLinesPerPage = 4
                                         });
                if (count == 0) { args.Player.SendMessage("No messages found.", Color.LightBlue); }
                return;
            }

            if ((Int32.TryParse(args.Parameters[0], out output)) && (args.Parameters.Count == 1))
            {
                bool found = false;
                int id = Convert.ToInt32(args.Parameters[0]);
                using (var reader = MessengerDB.QueryReader("SELECT * FROM Messages"))
                {

                    while (reader.Read())
                    {
                        if ((reader.Get<int>("ID") == id) && (reader.Get<String>("Receiver") == args.Player.Name) )
                        {
                            found = true;
                            args.Player.SendMessage("[" + reader.Get<string>("Time").Remove(19) + "] ", Color.LightBlue);
                            args.Player.SendMessage("* "+ reader.Get<string>("Sender") +": " + reader.Get<string>("Message"), Color.LightBlue);
                        }
                    }
                }
                if (found)
                {
                    var update = MessengerDB.Query("UPDATE Messages SET Status=@0 WHERE id= @1;", "Read", id);
                    
                    return;
                }
                if (!found)
                {
                    
                    args.Player.SendMessage("No message is found with that ID.", Color.LightBlue);
                    return;
                }
            }

            if (args.Parameters[0] == "del")
            {
                if ((args.Parameters.Count == 1) || (!Int32.TryParse(args.Parameters[1],out output)))
                {
                    args.Player.SendMessage("Invalid Syntax or ID: /msg del <ID>", Color.LightBlue);
                    return;
                }
                bool found = false;
                int id = Convert.ToInt32(args.Parameters[1]);
                using (var reader = MessengerDB.QueryReader("SELECT * FROM Messages"))
                {

                    while (reader.Read())
                    {
                        if ((reader.Get<int>("ID") == id) && (reader.Get<String>("Receiver") == args.Player.Name) && (reader.Get<String>("Status") != "Deleted"))
                        {
                            found = true;
                        }
                    }
                }
                if (found)
                {
                    var update = MessengerDB.Query("UPDATE Messages SET Status=@0 WHERE id= @1;", "Deleted", id);
                    args.Player.SendMessage("The Message has been deleted.", Color.LightBlue);
                    return;
                }
                if (!found)
                {

                    args.Player.SendMessage("No message is found with that ID.", Color.LightBlue);
                    return;
                }
            }

            #region addmsg
            
            if ((TShock.Users.GetUsersByName(args.Parameters[0]).Count == 0))
            {
                args.Player.SendMessage("No player with the name of \"" + args.Parameters[0] + "\" is found.", Color.LightBlue);
                return;
            }
            User plyname = TShock.Users.GetUsersByName(args.Parameters[0])[0];
            string message = args.Parameters[1];
            if (args.Parameters.Count == 1)
            {
                args.Player.SendMessage("Message is empty.", Color.LightBlue);
                return;
            }
            if ((args.Parameters.Count > 2))
            {
                message = args.Parameters[1];
                for (int i = 2; i < args.Parameters.Count; i++)
                {
                    message = message + " " + args.Parameters[i];
                }
            }
            var add = MessengerDB.Query("INSERT INTO Messages (Time, Sender, Receiver, Message, WorldID, Status) VALUES (@0, @1, @2, @3, @4, @5);", DateTime.Now, args.Player.Name, plyname.Name, message, Main.worldID, "Unread");
            if ((TShock.Utils.FindPlayer(plyname.Name).Count == 0))
            {
                args.Player.SendMessage("Player " + plyname.Name + " is not online. They will be notified about your message on their next login.", Color.LightBlue);              
                return;
            }
            if ((TShock.Utils.FindPlayer(plyname.Name).Count != 0))
            {
                if (((TShock.Utils.FindPlayer(plyname.Name)[0].RealPlayer)) && ((TShock.Utils.FindPlayer(plyname.Name)[0].ConnectionAlive)))
                {
                    TSPlayer receiver = TShock.Utils.FindPlayer(plyname.Name)[0];
                    args.Player.SendMessage("Player " + plyname.Name + " is online. They will be notified about your message.", Color.LightBlue);
                    int notiid = 0;
                    using (var reader = MessengerDB.QueryReader("SELECT * FROM Messages"))
                    {
                        while (reader.Read())
                        {
                            if ((reader.Get<string>("Receiver") == plyname.Name) && (reader.Get<string>("Status") == "Unread") && (reader.Get<string>("Sender") == args.Player.Name) && (reader.Get<int>("WorldID") == Main.worldID) && (reader.Get<string>("Message") == message)) 
                            {
                                notiid = reader.Get<int>("ID");
                            }
                        }
                    }
                    receiver.SendMessage("[Messenger] Player " + args.Player.Name + " has send you a message. Read it now using /msg "+notiid, Color.LightBlue);
                    return;
                }
            }
            #endregion addmsg

        }
    }
}
