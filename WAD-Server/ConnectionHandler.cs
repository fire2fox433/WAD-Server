﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace WAD_Server
{
    class ConnectionHandler
    {
        private Socket client;
        private NetworkStream ns;
        private StreamReader reader;
        private StreamWriter writer;
        private static int connections = 0;
        private Form1 f;

        // Requires params socket client and form f to initalize
        public ConnectionHandler(Socket client, Form1 f)
        {
            this.client = client;
            this.f = f;
        }

        public void HandleConnection(Object state)
        {
            try
            {
                ns = new NetworkStream(client);
                reader = new StreamReader(ns);
                writer = new StreamWriter(ns);
                connections++;

                f.SetText("New client accepted : " + connections + " active connections.");

                string input;
                while (true)
                {
                    // Readline inputs from client request
                    input = reader.ReadLine();

                    if (input.ToLower() == "login")
                    {
                        Authorize();
                    }
                    else if (input.ToLower() == "request_movie")
                    {
                        SendMovieList();
                    }
                    else if (input.ToLower() == "request_showtime")
                    {
                        SendMovieShowTime();
                    }
                    else if (input.ToLower() == "book_movie")
                    {
                        AddClientBooking();
                    }
                    else if (input.ToLower() == "view_booking")
                    {
                        ViewClientBooking();
                    }
                    else if (input.ToLower() == "search_movie")
                    {
                        SearchMovie();
                    }
                    else if (input.ToLower() == "register")
                    {
                        Register();
                    }
                    else if (input.ToLower() == "remove_booking")
                    {
                        removeBooking();
                    }
                    //else if (input.ToLower() == "terminate")
                    //    break;
                }
                ns.Close();
                client.Close();
                connections--;
                f.SetText("Client disconnected : " + connections + " active connections.");
            }
            catch (Exception)
            {
                connections--;
                f.SetText("Client disconnected : " + connections + " active connections.");
            }
        }

        // To authorize client login attempt
        public void Authorize()
        {
            ns = new NetworkStream(client);
            writer = new StreamWriter(ns);
            writer.AutoFlush = true;

            try
            {
                string email = reader.ReadLine();
                string password = reader.ReadLine();
                bool authorized = false;
                
                foreach (user details in variables.userList)
                {
                    // Check if email and password match in user hash set
                    if ((details.getEmail() == email) && (details.getPassword() == password))
                    {
                        authorized = true;
                        writer.WriteLine("authorized");
                        writer.WriteLine(details.getFirstName());
                        writer.WriteLine(details.getMiddleName());
                        writer.WriteLine(details.getLastName());
                        writer.WriteLine(details.getDOB());
                        writer.Flush();
                        break;
                    }
                }

                if (!authorized)
                {
                    writer.WriteLine("unauthorized");
                    writer.Flush();
                }
            }
            catch (Exception)
            {
                f.SetText("Exception occured on login");
            }
        }

        // To register client as new user
        public void Register()
        {
            ns = new NetworkStream(client);
            writer = new StreamWriter(ns);
            writer.AutoFlush = true;

            try
            {
                // Reads user properties
                string email = reader.ReadLine();
                string password = reader.ReadLine();
                string firstName = reader.ReadLine();
                string middleName = reader.ReadLine();
                string lastName = reader.ReadLine();
                string dob = reader.ReadLine();

                user newUser = new user();
                newUser.intializeUser(firstName, middleName, lastName, email, password, dob);
                bool added = false;
                // Lock user hash set before add operation
                lock (variables.userList) 
                {
                    added = variables.userList.Add(newUser);
                }
                // If add returns a false
                if (!added)
                {
                    writer.WriteLine("fail");
                    return;
                }
                else
                    writer.WriteLine("success");
            }
            catch (Exception)
            {
                writer.WriteLine("fail");
                f.SetText("Exception occured on register.");
            }
        }

        // Send list of movies to client
        public void SendMovieList()
        {
            ns = new NetworkStream(client);
            writer = new StreamWriter(ns);
            writer.AutoFlush = true;

            try
            {
                // Serialize movie hash set with class Movie properties
                var xs = new XmlSerializer(typeof(HashSet<Movie>));
                string xml;
                using (var write = new StringWriter())
                {
                    xs.Serialize(write, variables.movieList);
                    xml = write.ToString();
                    writer.WriteLine(xml);
                    writer.WriteLine("endofxml");
                }
            }
            catch (Exception)
            {
                writer.WriteLine("fail");
                f.SetText("Exception occured when sending movie list.");
            }
        }

        // Send movie show time property
        public void SendMovieShowTime()
        {
            ns = new NetworkStream(client);
            writer = new StreamWriter(ns);
            reader = new StreamReader(ns);
            writer.AutoFlush = true;
            try
            {
                // Reads movie title that client has selected
                string movie = reader.ReadLine();
                List<string> newList = new List<string>();
                string[] seats;
                string seat = null;
                // Iterates through movie hash set to find matching title
                foreach (Movie m in variables.movieList)
                {
                    if (movie == m.Title)
                    {
                        // Converts concurrent dictionary to purely a string and add to list
                        foreach (var showtime in m.ShowTime)
                        {
                            seats = showtime.Value;
                            seat =  showtime.Key + ";" + String.Join("|", seats);
                            newList.Add(seat);
                        }
                        break;
                    }
                }
                // Serialize List<string> of show times to be sent to client
                var xs = new XmlSerializer(typeof(List<string>));
                string xml;

                using (var write = new StringWriter())
                {
                    xs.Serialize(write, newList);
                    xml = write.ToString();
                    writer.WriteLine(xml);
                    writer.WriteLine("endofxml");
                }
            }
            catch (Exception)
            {
                writer.WriteLine("fail");
                f.SetText("Exception occured when sending movie show times.");
            }
        }

        // Add booking to booking list
        public void AddClientBooking()
        {
            ns = new NetworkStream(client);
            reader = new StreamReader(ns);
            writer = new StreamWriter(ns);
            writer.AutoFlush = true;
            try
            {
                string id = reader.ReadLine();
                string movie = reader.ReadLine();
                string user = reader.ReadLine();
                double price = Convert.ToDouble(reader.ReadLine());
                string date = reader.ReadLine();
                string time = reader.ReadLine();
                string[] seats = (reader.ReadLine()).Split('|');

                int count = 0;

                foreach (Movie m in variables.movieList)
                {
                    if (m.Title == movie)
                    {
                        string[] bookedSeats = m.ShowTime[date + ";" + time];

                        if (bookedSeats == null || bookedSeats.Length == 0)
                        {
                            writer.WriteLine("fail");
                            writer.WriteLine("All seats are being reserved!");
                            return;
                        }

                        List<string> list = new List<string>(bookedSeats);
                        foreach (string s in seats)
                        {
                            if (list.Contains(s))
                            {
                                count++;
                            }
                        }
                        // Check to see if no. of reserve seats matches no. avail seats
                        if (count == seats.Length)
                        {
                            foreach (string s in seats)
                            {
                                if (list.Contains(s))
                                {
                                    list.Remove(s);
                                }
                            }
                            // converts back to string[] and update ShowTime
                            m.ShowTime[date + ";" + time] = list.ToArray();
                        }
                        else
                        {
                            writer.WriteLine("fail");
                            //writer.WriteLine("Seats selected are already reserved!");
                            return;
                        }
                        break;
                    }
                }

                Booking newBook = new Booking();
                newBook.initBooking(id, movie, user, price, date, time, seats);

                // Hashset collection will prevent duplicates, lock statement before carrying out operation
                lock (variables.bookingList) variables.bookingList.Add(newBook);
                f.SetText("New booking added to Booking List.");
            }
            catch (Exception)
            {
                writer.WriteLine("fail");
                f.SetText("Exception occured when adding client booking.");
            }
        }

        // To return list of booking that client has booked
        public void ViewClientBooking()
        {
            ns = new NetworkStream(client);
            reader = new StreamReader(ns);
            writer = new StreamWriter(ns);
            writer.AutoFlush = true;
            try
            {
                string user = reader.ReadLine();
                bool found = false;
                HashSet<Booking> newSet = new HashSet<Booking>();

                foreach (Booking details in variables.bookingList)
                {
                    if (details.User == user)
                    {
                        found = true;
                        newSet.Add(details);
                    }
                }
                if (found)
                {
                    // If found, serialize newSet into XML to be sent to client
                    var xs = new XmlSerializer(typeof(HashSet<Booking>));
                    string xml;
                    using (var write = new StringWriter())
                    {
                        xs.Serialize(write, newSet);
                        xml = write.ToString();
                        writer.WriteLine(xml);
                        writer.WriteLine("endofxml");
                    }
                }
                else
                {
                    writer.WriteLine("fail");
                }
            }
            catch (Exception)
            {
                f.SetText("Exception occured when viewing client booking.");
            }
        }

        // To search movie list based on client input
        public void SearchMovie()
        {
            ns = new NetworkStream(client);
            reader = new StreamReader(ns);
            writer = new StreamWriter(ns);
            writer.AutoFlush = true;

            try
            {
                string input = reader.ReadLine();
                bool found = false;
                HashSet<Movie> newSet = new HashSet<Movie>();

                foreach (Movie details in variables.movieList)
                {
                    // If the title matches user input or input matches title first few characters
                    if (details.Title.ToLower() == input.ToLower() || details.Title.StartsWith(input.ToLower()))
                    {
                        // If Movie is showing, send info. (?)
                        if (details.Status == true)
                        {
                            found = true;
                            newSet.Add(details);
                        }
                    }
                }
                if (found)
                {
                    // If found, serialize newSet into XML to be sent to client
                    var xs = new XmlSerializer(typeof(HashSet<Movie>));
                    string xml;
                    using (var write = new StringWriter())
                    {
                        xs.Serialize(write, newSet);
                        xml = write.ToString();
                        writer.WriteLine(xml);
                        writer.WriteLine("endofxml");
                    }
                }
                else
                {
                    writer.WriteLine("fail");
                }
            }
            catch (Exception)
            {
                f.SetText("Exception occured when searching movie.");
            }
        }

        // To remove client's booking
        public void removeBooking()
        {
            ns = new NetworkStream(client);
            reader = new StreamReader(ns);
            writer = new StreamWriter(ns);
            writer.AutoFlush = true;

            try
            {
                string id = reader.ReadLine();
                //string movieTitle = null;
                //string[] clientSeats;
                var clientBooking = new Booking();
                foreach (Booking b in variables.bookingList)
                {
                    if (b.TransactionId == id)
                    {
                        clientBooking = b;
                        //movieTitle = b.Movie;
                        //clientSeats = b.Seats;
                        break;
                    }
                }

                foreach (Movie m in variables.movieList)
                {
                    if (m.Title == clientBooking.Movie)
                    {
                        string[] bookedSeats = m.ShowTime[clientBooking.Date + ";" + clientBooking.Timeslot];
                        List<string> list = new List<string>(bookedSeats);
                        List<string> tempList = clientBooking.Seats.OfType<string>().ToList();
                        list.AddRange(tempList);
                        //foreach (string s in clientBooking.Seats)
                        //{
                        //    list.Add(s);
                        //}
                        // Locks movie hash set before setting the value
                        lock (variables.movieList) m.ShowTime[clientBooking.Date + ";" + clientBooking.Timeslot] = list.ToArray();
                        lock (variables.bookingList) variables.bookingList.Remove(clientBooking);
                        f.SetText("Client's booking has been removed from Booking List.");
                        break;
                    }
                }

                //string movie = reader.ReadLine();
                //string user = reader.ReadLine();
                //double price = Convert.ToDouble(reader.ReadLine());
                //string date = reader.ReadLine();
                //string time = reader.ReadLine();
                //string[] seats = (reader.ReadLine()).Split('|');

                //foreach (Movie m in variables.movieList)
                //{
                //    if (m.Title == movie)
                //    {
                //        string[] bookedSeats = m.ShowTime[date + ";" + time];
                //        List<string> list = new List<string>(bookedSeats);
                //        foreach (string s in seats)
                //        {
                //            list.Add(s);
                //        }
                //        // Locks movie hash set before setting the value
                //        lock (variables.movieList) m.ShowTime[date + ";" + time] = list.ToArray();
                //        break;
                //    }
                //}

                //Booking newBook = new Booking();
                //newBook.initBooking(id, movie, user, price, date, time, seats);
                //// Lock hash set before carrying out operation
                //lock (variables.bookingList) variables.bookingList.Remove(newBook);
                //f.SetText("Client's booking has been removed from Booking List.");
            }
            catch
            {
                f.SetText("Exception occured when removing client's booking.");
            }
        }
    }
}
