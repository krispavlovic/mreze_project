using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace MainServer
{
    public class Stavka
    {
        public int ID { get; set; }
        public string naziv { get; set; }
        public int kolicina { get; set; }
        public int tip { get; set; }
        public double cena { get; set; }
        public int status { get; set; }
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
        // samo za kuvare/barmene
        public string? uloga { get; set; }
        public List<Stavka>? stackStavki { get; set; }
        public int? stavkaID { get; set; }
        public int? stavkaUpdate { get; set; }
        public List<Stavka>? notifikacijaGotovePorudzbine { get; set; }
        public Rezervacija? rezervacija { get; set; }
    }

    public class Sto
    {
        public int broj { get; set; } // broj stola
        public int brojGostiju { get; set; }
        public bool zauzet { get; set; } = false;

        public List<Stavka> porudzbine { get; set; } = new List<Stavka>();
        public double? racunSaPDV { get; set; } = 0;
    }

    public class Rezervacija
    {
        public int rezervacijaID { get; set; }
        public DateTime rezervacijaVreme { get; set; }
        public int brojStola { get; set; }
        public int brojGostiju { get; set; }
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
            var tcpKuvarKlijenti = new List<Socket>();
            var tcpBarmenKlijenti = new List<Socket>();
            List<Sto> stolovi = new List<Sto>();
            List<Rezervacija> rezervacije = new List<Rezervacija>();
            List<Stavka> stackHrana = new List<Stavka>();
            List<Stavka> stackPice = new List<Stavka>();
            List<Stavka> notifikacijaPorudzbine = new List<Stavka>();

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
                        }else if (tip == "kreiraj_rezervaciju")
                        {
                            string odgovor;

                            Poruka poruka = JsonSerializer.Deserialize<Poruka>(json);

                            Sto sto = stolovi.Find(s => s.broj == poruka.sto);

                            if (sto != null && sto.zauzet)
                            {
                                odgovor = $"Sto {poruka.sto} je već rezervisan!";
                            }
                            else
                            {
                                rezervacije.Add(poruka.rezervacija);
                                odgovor = $"Rezervacija za sto {poruka.sto} je uspešno evidentirana sa {poruka.brojGostiju} gostiju u {poruka.rezervacija.rezervacijaVreme}.";
                            }

                            byte[] odgovorBytes = Encoding.UTF8.GetBytes(odgovor);
                            udpSocket.SendTo(odgovorBytes, udpClientEP);
                            Console.WriteLine($"Rezervacija za sto {poruka.sto} je uspešno evidentirana sa {poruka.brojGostiju} gostiju u {poruka.rezervacija.rezervacijaVreme}.");
                        }
                        else if (tip == "azuriraj_rezervaciju")
                        {
                            string odgovor;

                            Poruka poruka = JsonSerializer.Deserialize<Poruka>(json);

                            Rezervacija rezervacija = rezervacije.Find(r => r.rezervacijaID == poruka.rezervacija.rezervacijaID);

                            if (rezervacija != null)
                            {
                                rezervacija.rezervacijaVreme = poruka.rezervacija.rezervacijaVreme;
                                rezervacija.brojStola = poruka.rezervacija.brojStola;
                                rezervacija.brojGostiju = poruka.rezervacija.brojGostiju;

                                odgovor = $"Rezervacija ID:{poruka.rezervacija.rezervacijaID} je azurirana!";
                            }
                            else
                            {
                                odgovor = $"Rezervacija pod ID:{poruka.rezervacija.rezervacijaID} nije pronadjena!";
                            }

                            byte[] odgovorBytes = Encoding.UTF8.GetBytes(odgovor);
                            udpSocket.SendTo(odgovorBytes, udpClientEP);
                            Console.WriteLine($"Rezervacija za sto {poruka.sto} je uspešno evidentirana sa {poruka.brojGostiju} gostiju u {poruka.rezervacija.rezervacijaVreme}.");
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

                                        if(stavka.tip == 1) // HRANA
                                        {
                                            stackHrana.Add(stavka);
                                        }else if (stavka.tip == 2)
                                        {
                                            stackPice.Add(stavka); // PICE
                                        }

                                        Console.WriteLine($"Naziv: {stavka.naziv}, Kolicina: {stavka.kolicina}, Cena(kom): {stavka.cena}");
                                    }
                                    poruka = new Poruka
                                    {
                                        tip = "potvrda",
                                        poruka = "Uspesno dodavanje porudzbine!",
                                        notifikacijaGotovePorudzbine = notifikacijaPorudzbine
                                    };
                                }
                                else
                                {
                                    poruka = new Poruka
                                    {
                                        tip = "greska",
                                        poruka = "Izabrani sto nije zauzet",
                                        notifikacijaGotovePorudzbine = notifikacijaPorudzbine
                                    };
                                }

                                byte[] porukaBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(poruka));
                                klijent.Send(porukaBytes);
                                notifikacijaPorudzbine.Clear();

                                Poruka odgovorKuvar = new Poruka
                                {
                                    tip = "stack_update",
                                    stackStavki = stackHrana,
                                };

                                Poruka odgovorBarmen = new Poruka
                                {
                                    tip = "stack_update_barmen",
                                    stackStavki = stackPice,
                                };

                                byte[] odgovorKuvarBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(odgovorKuvar) + "\n"); // delimiter
                                byte[] odgovorBarmenBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(odgovorBarmen) + "\n"); // delimiter

                                foreach (Socket kuvarKlijent in tcpKuvarKlijenti)
                                {
                                    kuvarKlijent.Send(odgovorKuvarBytes);
                                }

                                foreach (Socket barmenKlijent in tcpBarmenKlijenti)
                                {
                                    barmenKlijent.Send(odgovorBarmenBytes);
                                }

                            }
                            else if (tip == "racun")
                            {
                                Sto sto = stolovi.Find(s => s.broj == brojStola);
                                Poruka poruka;

                                Console.WriteLine($"[TCP] Zahtev za račun sa stola {brojStola}.");

                                bool porudzbineGotove = true;

                                if(sto != null)
                                {
                                    foreach (Stavka porudzbina in sto.porudzbine)
                                    {
                                        if(porudzbina.status != 3)
                                        {
                                            porudzbineGotove = false;
                                            break;
                                        }
                                    }
                                }

                                if (sto != null && sto.zauzet && porudzbineGotove)
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
                                    Console.WriteLine($" - Racun uz PDV: {Math.Round(racunSaPDV, 2)}");
                                    poruka = new Poruka
                                    {
                                        tip = "potvrda",
                                        poruka = $"Racun: {racun} din, PDV: 20%, Racun sa PDV-om: {Math.Round(racunSaPDV, 2)} din, Napojnica 15%: {Math.Round(racunSaPDV * 0.15)} din",
                                        notifikacijaGotovePorudzbine = notifikacijaPorudzbine
                                    };
                                    sto.racunSaPDV = Math.Round(racunSaPDV, 2);
                                }
                                else
                                {
                                    poruka = new Poruka
                                    {
                                        tip = "greska",
                                        poruka = "Izabrani sto nije zauzet ili porudzbine nisu gotove",
                                        notifikacijaGotovePorudzbine = notifikacijaPorudzbine
                                    };
                                }
                                Console.WriteLine($"[TCP] Zahtev za račun sa stola {brojStola}.");

                                byte[] porukaBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(poruka));
                                klijent.Send(porukaBytes);
                                notifikacijaPorudzbine.Clear();
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
                                        poruka = $"Uplacena suma je nedovoljna, fali {sto.racunSaPDV - poruka.uplata} din",
                                        notifikacijaGotovePorudzbine = notifikacijaPorudzbine
                                    };
                                }
                                else
                                {
                                    poruka = new Poruka
                                    {
                                        tip = "potvrda",
                                        poruka = $"Uplata je uspesna! Kusur: {poruka.uplata - sto.racunSaPDV} din",
                                        notifikacijaGotovePorudzbine = notifikacijaPorudzbine
                                    };
                                    stolovi.Remove(sto);
                                }

                                byte[] porukaBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(poruka));
                                klijent.Send(porukaBytes);
                                notifikacijaPorudzbine.Clear();
                            }
                            else if (tip == "stanje_restorana")
                            {
                                Console.WriteLine($"Zahtev za stanje restorana.");

                                var odgovor = new
                                {
                                    tip = "stanje_restorana",
                                    stolovi = stolovi,
                                    notifikacijaGotovePorudzbine = notifikacijaPorudzbine
                                };

                                byte[] odgovorBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(odgovor));
                                klijent.Send(odgovorBytes);
                                notifikacijaPorudzbine.Clear();
                            }

                            else if (tip == "stanje_porudzbina")
                            {
                                Console.WriteLine($"Zahtev za stanje porudzbina.");

                                List<Stavka> tempPorudzbine = new List<Stavka>();

                                foreach(Sto sto in stolovi)
                                {
                                    foreach(Stavka porudzbina in sto.porudzbine)
                                    {
                                        tempPorudzbine.Add(porudzbina);
                                    }
                                }

                                var odgovor = new
                                {
                                    tip = "stanje_porudzbina",
                                    porudzbine = tempPorudzbine,
                                    notifikacijaGotovePorudzbine = notifikacijaPorudzbine
                                };

                                byte[] odgovorBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(odgovor));
                                klijent.Send(odgovorBytes);
                                notifikacijaPorudzbine.Clear();
                            }

                            // KUVAR / BARMEN

                            else if (tip == "prijava")
                            {
                                Poruka poruka = JsonSerializer.Deserialize<Poruka>(json);
                                if(poruka.uloga == "kuvar")
                                {
                                    tcpKuvarKlijenti.Add(klijent);

                                    Poruka odgovor = new Poruka
                                    {
                                        tip = "stack_update",
                                        stackStavki = stackHrana
                                    };

                                    byte[] odgovorBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(odgovor) + "\n");

                                    klijent.Send(odgovorBytes);
                                }else if (poruka.uloga == "barmen")
                                {
                                    tcpBarmenKlijenti.Add(klijent);

                                    Poruka odgovor = new Poruka
                                    {
                                        tip = "stack_update_barmen",
                                        stackStavki = stackPice
                                    };

                                    byte[] odgovorBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(odgovor) + "\n");

                                    klijent.Send(odgovorBytes);
                                }
                            }

                            else if (tip == "update_stavka")
                            {
                                Poruka poruka = JsonSerializer.Deserialize<Poruka>(json);

                                stackHrana.RemoveAll(s => s.ID == poruka.stavkaID);

                                foreach(Sto sto in stolovi)
                                {
                                    Stavka updatedStavka = sto.porudzbine.Find(p => p.ID == poruka.stavkaID);
                                    
                                    if(updatedStavka != null)
                                    {
                                        updatedStavka.status = (int)poruka.stavkaUpdate;
                                        if((int)poruka.stavkaUpdate == 3)
                                        {
                                            notifikacijaPorudzbine.Add(updatedStavka);
                                        }
                                    }
                                }
                                
                            }
                            // MENADZERI
                            else if (tip == "izlistaj_rezervacije")
                            {
                                Poruka poruka = JsonSerializer.Deserialize<Poruka>(json);

                                var odgovor = new
                                {
                                    tip = "izlistaj_rezervacije",
                                    rezervacije = rezervacije
                                };

                                byte[] odgovorBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(odgovor));
                                klijent.Send(odgovorBytes);
                                notifikacijaPorudzbine.Clear();
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
