using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace MainServer
{
    public class Stavka
    {
        public string naziv { get; set; }
        public int kolicina { get; set; }
        public double cena { get; set; }
    }
    public class Poruka
    {
        public string tip { get; set; }         // "dolazak", "porudzbina", "racun"
        public int sto { get; set; }

        // Samo za "dolazak"
        public int? brojGostiju { get; set; }

        // Samo za "porudzbina"
        public List<Stavka> stavke { get; set; }
        public string? poruka { get; set; }
        public double? uplata { get; set; }
    }

    public class Sto
    {
        public int broj { get; set; } // broj stola
        public int brojGostiju { get; set; }
        public bool zauzet { get; set; } = false;

        public List<Stavka> porudzbine { get; set; } = new List<Stavka>();
        public double? racunSaPDV { get; set; } = 0;
    }

    internal class Server
    {
        static void Main(string[] args)
        {
            Socket udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            IPEndPoint udpEndpoint = new IPEndPoint(IPAddress.Any, 15000);
            udpSocket.Bind(udpEndpoint);
            EndPoint udpClientEP = new IPEndPoint(IPAddress.Any, 0);

            // --- TCP socket za porudžbine i račune ---
            Socket tcpListener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint tcpEndpoint = new IPEndPoint(IPAddress.Any, 16000);
            tcpListener.Bind(tcpEndpoint);
            tcpListener.Listen(10);
            tcpListener.Blocking = false;

            Console.WriteLine("Server pokrenut");
            Console.WriteLine("UDP port: 15000 | TCP port: 16000\n");
         
            // Lista prihvaćenih TCP klijenata
            var tcpKlijenti = new List<Socket>();
            List<int> zauzetiStolovi = new List<int>();
            List<Sto> stolovi = new List<Sto>();

            byte[] buffer = new byte[4096];

            while (true)
            {
                // === UDP polling ===
                if (udpSocket.Poll(1000 * 1000, SelectMode.SelectRead)) // 1 sekunda
                {
                    try
                    {
                        int brBajta = udpSocket.ReceiveFrom(buffer, ref udpClientEP);
                        string json = Encoding.UTF8.GetString(buffer, 0, brBajta);
                        var data = JsonSerializer.Deserialize<JsonElement>(json);
                        string tip = data.GetProperty("tip").GetString();

                        if (tip == "dolazak")
                        {
                            int brojStola = data.GetProperty("sto").GetInt32();
                            int brojGostiju = data.GetProperty("brojGostiju").GetInt32();
                            string odgovor;

                            Sto sto = stolovi.Find(s => s.broj == brojStola);

                            if (sto != null && sto.zauzet)
                            {
                                odgovor = $"Sto {brojStola} je već zauzet!";
                            }
                            else
                            {
                                stolovi.Add(new Sto { broj = brojStola, porudzbine = new List<Stavka>(), zauzet = true, brojGostiju = brojGostiju });
                                odgovor = $"Sto {brojStola} je uspešno evidentiran sa {brojGostiju} gostiju.";
                            }

                            byte[] odgovorBytes = Encoding.UTF8.GetBytes(odgovor);
                            udpSocket.SendTo(odgovorBytes, udpClientEP);
                            Console.WriteLine($"[UDP] Sto {brojStola} zauzet sa {brojGostiju} gostiju.");
                        }
                        
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"UDP greška: {ex.Message}");
                    }
                }

                // === TCP prihvatanje novih konekcija ===
                if (tcpListener.Poll(500 * 1000, SelectMode.SelectRead)) // 0.5 sekunde
                {
                    try
                    {
                        Socket noviKlijent = tcpListener.Accept();
                        noviKlijent.Blocking = false;
                        tcpKlijenti.Add(noviKlijent);

                        Console.WriteLine($"Novi TCP klijent povezan: {noviKlijent.RemoteEndPoint}");
                    }
                    catch (SocketException) { /* Neko je poll-ovan, ali nije još spreman */ }
                }

                // === TCP obrada postojećih klijenata ===
                for (int i = tcpKlijenti.Count - 1; i >= 0; i--)
                {
                    Socket klijent = tcpKlijenti[i];

                    if (klijent.Poll(100 * 1000, SelectMode.SelectRead)) // 0.1 sekunde
                    {
                        try
                        {
                            int brBajta = klijent.Receive(buffer);
                            if (brBajta == 0)
                            {
                                klijent.Close();
                                tcpKlijenti.RemoveAt(i);
                                continue;
                            }

                            string json = Encoding.UTF8.GetString(buffer, 0, brBajta);
                            var data = JsonSerializer.Deserialize<JsonElement>(json);

                            string tip = data.GetProperty("tip").GetString();
                            int brojStola = data.GetProperty("sto").GetInt32();

                            if (tip == "porudzbina")
                            {
                                Poruka poruka = JsonSerializer.Deserialize<Poruka>(json);
                                Sto sto = stolovi.Find(s => s.broj == brojStola);

                                if (sto != null && sto.zauzet)
                                {
                                    Console.WriteLine($"[TCP] Porudžbina za sto {brojStola}:");
                                    foreach (var stavka in poruka.stavke)
                                    {
                                        sto.porudzbine.Add(stavka);
                                        Console.WriteLine($"Naziv: {stavka.naziv}, Kolicina: {stavka.kolicina}, Cena(kom): {stavka.cena}");
                                    }
                                    poruka = new Poruka
                                    {
                                        tip = "potvrda",
                                        poruka = "Uspesno dodavanje porudzbine!"
                                    };
                                }
                                else
                                {
                                    poruka = new Poruka
                                    {
                                        tip = "greska",
                                        poruka = "Izabrani sto nije zauzet"
                                    };
                                }

                                byte[] porukaBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(poruka));
                                klijent.Send(porukaBytes);

                            }
                            else if (tip == "racun")
                            {
                                Sto sto = stolovi.Find(s => s.broj == brojStola);
                                Poruka poruka;

                                Console.WriteLine($"[TCP] Zahtev za račun sa stola {brojStola}.");

                                if (sto != null && sto.zauzet)
                                {
                                    double racun = 0;
                                    double racunSaPDV = 0;

                                    foreach (Stavka stavka in sto.porudzbine)
                                    {
                                        racun += stavka.cena * stavka.kolicina;
                                    }

                                    Console.WriteLine($" - Racun: {Math.Round(racun, 2)}");
                                    racunSaPDV = (racun * 0.2) + racun;
                                    Console.WriteLine($" - PDV: 20%");
                                    Console.WriteLine($" - Racun uz PDV: {Math.Round(racun, 2)}");
                                    poruka = new Poruka
                                    {
                                        tip = "potvrda",
                                        poruka = $"Racun: {racun} din, PDV: 20%, Racun sa PDV-om: {Math.Round(racunSaPDV, 2)} din, Napojnica 15%: {Math.Round(racunSaPDV * 0.15)} din"
                                    };
                                    sto.racunSaPDV = Math.Round(racunSaPDV, 2);
                                }
                                else
                                {
                                    poruka = new Poruka
                                    {
                                        tip = "greska",
                                        poruka = "Izabrani sto nije zauzet"
                                    };
                                }
                                Console.WriteLine($"[TCP] Zahtev za račun sa stola {brojStola}.");

                                byte[] porukaBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(poruka));
                                klijent.Send(porukaBytes);
                            }
                            else if (tip == "uplata")
                            {
                                Sto sto = stolovi.Find(s => s.broj == brojStola);
                                Poruka poruka = JsonSerializer.Deserialize<Poruka>(json);

                                if(sto.racunSaPDV > poruka.uplata)
                                {
                                    poruka = new Poruka
                                    {
                                        tip = "greska",
                                        poruka = $"Uplacena suma je nedovoljna, fali {sto.racunSaPDV - poruka.uplata} din"
                                    };
                                }
                                else
                                {
                                    poruka = new Poruka
                                    {
                                        tip = "potvrda",
                                        poruka = $"Uplata je uspesna! Kusur: {poruka.uplata - sto.racunSaPDV} din"
                                    };
                                    stolovi.Remove(sto);
                                }

                                byte[] porukaBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(poruka));
                                klijent.Send(porukaBytes);
                            }
                            else if (tip == "stanje_restorana")
                            {
                                Console.WriteLine($"Zahtev za stanje restorana.");

                                var odgovor = new
                                {
                                    tip = "stanje_restorana",
                                    stolovi = stolovi,
                                };

                                byte[] odgovorBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(odgovor));
                                klijent.Send(odgovorBytes);
                            }
                        }
                        catch (SocketException)
                        {
                            // Nešto nije u redu — zatvaramo konekciju
                            klijent.Close();
                            tcpKlijenti.RemoveAt(i);
                        }
                    }
                }

                // Kratka pauza u glavnoj petlji
                Thread.Sleep(100);
            }
        }
    }
}
