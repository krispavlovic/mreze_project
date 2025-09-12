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
        public int tip { get; set; } // kategorija, hrana - 1 ili pice - 2
        public double cena { get; set; }
        public int status { get; set; } // ceka pripremu - 1, u pripremi - 2, pripremljeno - 3
    }
    public class Poruka
    {
        public string tip { get; set; }         // "dolazak", "porudzbina", "racun", itd
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
        public List<Rezervacija>? rezervacije { get; set; }
        public List<Stavka>? notifikacijaGotovePorudzbine { get; set; }
        public List<Rezervacija>? notifikacijaGotoveRezervacije { get; set; }
        public Rezervacija? rezervacija { get; set; }
        public List<Sto>? stolovi { get; set; }
    }

    public class Sto
    {
        public int broj { get; set; } // broj stola
        public int brojGostiju { get; set; }
        public string status { get; set; } = "slobodan";

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

    public class Osoblje
    {
        public string tip { get; set; }
        public bool zauzet { get; set; }
        public Socket tcpKonekcioniSocket { get; set; }
    }

    internal class Server
    {
        static void Main(string[] args)
        {
            Socket udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            IPEndPoint udpEndpoint = new IPEndPoint(IPAddress.Any, 15000);
            udpSocket.Bind(udpEndpoint);
            EndPoint udpClientEP = new IPEndPoint(IPAddress.Any, 0);

            // TCP socket za porudzbine i racune
            Socket tcpListener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint tcpEndpoint = new IPEndPoint(IPAddress.Any, 16000);
            tcpListener.Bind(tcpEndpoint);
            tcpListener.Listen(10);
            tcpListener.Blocking = false;

            Console.WriteLine("Server pokrenut");
            Console.WriteLine("UDP port: 15000 | TCP port: 16000\n");

            // Lista prihvacenih TCP klijenata
            List<Osoblje> osoblje = new List<Osoblje>();
            List<Socket> tcpKlijenti = new List<Socket>();

            // Restoran informacije
            List<Sto> stolovi = new List<Sto>();
            List<Rezervacija> rezervacije = new List<Rezervacija>();
            List<Stavka> stackHrana = new List<Stavka>();
            List<Stavka> stackPice = new List<Stavka>();
            List<Stavka> notifikacijaPorudzbine = new List<Stavka>();
            List<Rezervacija> notifikacijaRezervacije = new List<Rezervacija>();

            byte[] buffer = new byte[4096];

            while (true)
            {
                if (udpSocket.Poll(1000 * 1000, SelectMode.SelectRead)) // 1 sekunda
                {
                    try
                    {
                        int brBajta = udpSocket.ReceiveFrom(buffer, ref udpClientEP);
                        string json = Encoding.UTF8.GetString(buffer, 0, brBajta);
                        JsonElement data = JsonSerializer.Deserialize<JsonElement>(json);
                        string tip = data.GetProperty("tip").GetString();

                        if (tip == "dolazak")
                        {
                            int brojStola = data.GetProperty("sto").GetInt32();
                            int brojGostiju = data.GetProperty("brojGostiju").GetInt32();
                            string odgovor;

                            Sto sto = stolovi.Find(s => s.broj == brojStola);

                            if (sto != null && sto.status == "zauzet")
                            {
                                odgovor = $"Greska! Sto {brojStola} je već zauzet!";
                            }
                            else if (sto != null && sto.status == "rezervisan")
                            {
                                odgovor = $"Sto {brojStola} je uspešno evidentiran sa {brojGostiju} gostiju. Rezervacija za ovaj sto je ispunjena, brise se.";

                                sto.broj = brojStola;
                                sto.brojGostiju = brojGostiju;
                                sto.status = "zauzet";
                                rezervacije.RemoveAll(r => r.brojStola == sto.broj);
                            }
                            else
                            {
                                stolovi.Add(new Sto { broj = brojStola, porudzbine = new List<Stavka>(), status = "zauzet", brojGostiju = brojGostiju });
                                odgovor = $"Sto {brojStola} je uspešno evidentiran sa {brojGostiju} gostiju. ";
                            }
                            

                            byte[] odgovorBytes = Encoding.UTF8.GetBytes(odgovor);
                            udpSocket.SendTo(odgovorBytes, udpClientEP);
                            Console.WriteLine($"[UDP] Sto {brojStola} zauzet sa {brojGostiju} gostiju.\n");

                            printajStolove(stolovi);
                        }else if (tip == "kreiraj_rezervaciju")
                        {
                            string odgovor;

                            Poruka poruka = JsonSerializer.Deserialize<Poruka>(json);

                            Sto sto = stolovi.Find(s => s.broj == poruka.sto);

                            if (sto != null && sto.status == "zauzet")
                            {
                                odgovor = $"Greska! Sto {poruka.sto} je zauzet!";
                            }else if (sto != null && sto.status == "rezervisan")
                            {
                                odgovor = $"Greska! Sto {poruka.sto} je vec rezervisan!";
                            }
                            else
                            {
                                stolovi.Add(new Sto { broj = poruka.sto, porudzbine = new List<Stavka>(), status = "rezervisan", brojGostiju = (int)poruka.brojGostiju });
                                rezervacije.Add(poruka.rezervacija);
                                odgovor = $"Rezervacija za sto {poruka.sto} (ID:{poruka.rezervacija.rezervacijaID}) je uspešno evidentirana sa {poruka.brojGostiju} gostiju u {poruka.rezervacija.rezervacijaVreme}.";
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
                            Sto zeljeniSto = stolovi.Find(s => s.broj == poruka.rezervacija.brojStola);

                            if (rezervacija != null && zeljeniSto == null)
                            {
                                Sto sto = stolovi.Find(s => s.broj == rezervacija.brojStola);

                                if(sto != null)
                                {
                                    sto.broj = poruka.rezervacija.brojStola;
                                    sto.brojGostiju = poruka.rezervacija.brojGostiju;

                                    rezervacija.rezervacijaVreme = poruka.rezervacija.rezervacijaVreme;
                                    rezervacija.brojStola = poruka.rezervacija.brojStola;
                                    rezervacija.brojGostiju = poruka.rezervacija.brojGostiju;

                                    odgovor = $"Rezervacija ID:{poruka.rezervacija.rezervacijaID} je azurirana!";
                                }
                                else
                                {
                                    odgovor = "Sto sa ovom rezervacijom nije pronadjen.";
                                }
                            }
                            else if (zeljeniSto != null)
                            {
                                odgovor = "Sto na koji zelite da azurirate je vec rezervisan ili zauzet.";
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

                // TCP prihvatanje novih konekcija
                if (tcpListener.Poll(500 * 1000, SelectMode.SelectRead)) // 0.5 sekunde
                {
                    try
                    {
                        Socket noviKlijent = tcpListener.Accept();
                        noviKlijent.Blocking = false;
                        tcpKlijenti.Add(noviKlijent);

                        Console.WriteLine($"Novi TCP klijent povezan: {noviKlijent.RemoteEndPoint}");
                    }
                    catch (SocketException) { }
                }

                // TCP obrada postojećih klijenata
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

                            string batch = Encoding.UTF8.GetString(buffer, 0, brBajta);

                            batch = batch.Replace("}\r\n{", "}\n{");
                            batch = batch.Replace("}{", "}\n{");

                            string[] poruke = batch.Split('\n');

                            foreach (string json in poruke)
                            {
                                if (string.IsNullOrWhiteSpace(json)) continue;

                                JsonElement data = JsonSerializer.Deserialize<JsonElement>(json);

                                string tip = data.GetProperty("tip").GetString();
                                int brojStola = data.GetProperty("sto").GetInt32();

                                // KONOBAR
                                if (tip == "porudzbina")
                                {
                                    Poruka poruka = JsonSerializer.Deserialize<Poruka>(json);
                                    Sto sto = stolovi.Find(s => s.broj == brojStola);

                                    if (sto != null && sto.status == "zauzet")
                                    {
                                        Console.WriteLine($"[TCP] Porudžbina za sto {brojStola}:");
                                        foreach (Stavka stavka in poruka.stavke)
                                        {
                                            sto.porudzbine.Add(stavka);

                                            if (stavka.tip == 1) // HRANA
                                            {
                                                stackHrana.Add(stavka);
                                            }
                                            else if (stavka.tip == 2) // PICE
                                            {
                                                stackPice.Add(stavka); 
                                            }

                                            Console.WriteLine($"Naziv: {stavka.naziv}, Kolicina: {stavka.kolicina}, Cena(kom): {stavka.cena}");
                                        }
                                        poruka = new Poruka
                                        {
                                            tip = "potvrda",
                                            poruka = "Uspesno dodavanje porudzbine!",
                                            notifikacijaGotovePorudzbine = notifikacijaPorudzbine,
                                            notifikacijaGotoveRezervacije = notifikacijaRezervacije
                                        };
                                    }
                                    else
                                    {
                                        poruka = new Poruka
                                        {
                                            tip = "greska",
                                            poruka = "Izabrani sto nije zauzet",
                                            notifikacijaGotovePorudzbine = notifikacijaPorudzbine,
                                            notifikacijaGotoveRezervacije = notifikacijaRezervacije
                                        };
                                    }

                                    byte[] porukaBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(poruka));
                                    klijent.Send(porukaBytes);
                                    notifikacijaPorudzbine.Clear();
                                    notifikacijaRezervacije.Clear();

                                    foreach (Osoblje radnik in osoblje)
                                    {
                                        if (radnik.tip == "kuvar" && !radnik.zauzet && stackHrana.Count > 0)
                                        {
                                            Poruka odgovorKuvar = new Poruka
                                            {
                                                tip = "stack_update",
                                                stackStavki = stackHrana,
                                            };

                                            byte[] odgovorKuvarBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(odgovorKuvar) + "\n"); // + delimiter

                                            radnik.tcpKonekcioniSocket.Send(odgovorKuvarBytes);
                                            stackHrana.RemoveAt(stackHrana.Count - 1);
                                        }
                                        else if (radnik.tip == "barmen" && !radnik.zauzet && stackPice.Count > 0)
                                        {
                                            Poruka odgovorBarmen = new Poruka
                                            {
                                                tip = "stack_update_barmen",
                                                stackStavki = stackPice,
                                            };

                                            byte[] odgovorBarmenBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(odgovorBarmen) + "\n"); // + delimiter

                                            radnik.tcpKonekcioniSocket.Send(odgovorBarmenBytes);
                                            stackPice.RemoveAt(stackPice.Count - 1);
                                        }
                                    }

                                    PrintajPorudzbine(stolovi);

                                }
                                else if (tip == "racun")
                                {
                                    Sto sto = stolovi.Find(s => s.broj == brojStola);
                                    Poruka poruka;

                                    Console.WriteLine($"[TCP] Zahtev za račun sa stola {brojStola}.");

                                    bool porudzbineGotove = true;

                                    if (sto != null)
                                    {
                                        foreach (Stavka porudzbina in sto.porudzbine)
                                        {
                                            if (porudzbina.status != 3)
                                            {
                                                porudzbineGotove = false;
                                                break;
                                            }
                                        }
                                    }

                                    if (sto != null && sto.status == "zauzet" && porudzbineGotove)
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

                                    if (sto.racunSaPDV > poruka.uplata)
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
                                            notifikacijaGotovePorudzbine = notifikacijaPorudzbine,
                                            notifikacijaGotoveRezervacije = notifikacijaRezervacije
                                        };
                                        stolovi.Remove(sto);
                                    }

                                    byte[] porukaBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(poruka));
                                    klijent.Send(porukaBytes);
                                    notifikacijaPorudzbine.Clear();
                                    notifikacijaRezervacije.Clear();
                                    PrintajPorudzbine(stolovi);
                                    printajStolove(stolovi);
                                }
                                else if (tip == "stanje_restorana")
                                {
                                    Console.WriteLine($"Zahtev za stanje restorana.");

                                    Poruka odgovor = new Poruka
                                    {
                                        tip = "stanje_restorana",
                                        stolovi = stolovi,
                                        notifikacijaGotovePorudzbine = notifikacijaPorudzbine,
                                        notifikacijaGotoveRezervacije = notifikacijaRezervacije
                                    };

                                    byte[] odgovorBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(odgovor));
                                    klijent.Send(odgovorBytes);
                                    notifikacijaPorudzbine.Clear();
                                    notifikacijaRezervacije.Clear();
                                }

                                else if (tip == "stanje_porudzbina")
                                {
                                    Console.WriteLine($"Zahtev za stanje porudzbina.");

                                    List<Stavka> tempPorudzbine = new List<Stavka>();

                                    foreach (Sto sto in stolovi)
                                    {
                                        foreach (Stavka porudzbina in sto.porudzbine)
                                        {
                                            tempPorudzbine.Add(porudzbina);
                                        }
                                    }

                                    Poruka odgovor = new Poruka
                                    {
                                        tip = "stanje_porudzbina",
                                        stavke = tempPorudzbine,
                                        notifikacijaGotovePorudzbine = notifikacijaPorudzbine,
                                        notifikacijaGotoveRezervacije = notifikacijaRezervacije
                                    };

                                    byte[] odgovorBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(odgovor));
                                    klijent.Send(odgovorBytes);
                                    notifikacijaPorudzbine.Clear();
                                    notifikacijaRezervacije.Clear();
                                }

                                // KUVAR / BARMEN

                                else if (tip == "prijava")
                                {
                                    Poruka poruka = JsonSerializer.Deserialize<Poruka>(json);
                                    if (poruka.uloga == "kuvar")
                                    {
                                        osoblje.Add(new Osoblje { tip = "kuvar", zauzet = false, tcpKonekcioniSocket = klijent });

                                        Poruka odgovor = new Poruka
                                        {
                                            tip = "stack_update",
                                            stackStavki = stackHrana
                                        };

                                        byte[] odgovorBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(odgovor) + "\n");

                                        klijent.Send(odgovorBytes);
                                    }
                                    else if (poruka.uloga == "barmen")
                                    {
                                        osoblje.Add(new Osoblje { tip = "barmen", zauzet = false, tcpKonekcioniSocket = klijent });

                                        Poruka odgovor = new Poruka
                                        {
                                            tip = "stack_update_barmen",
                                            stackStavki = stackPice
                                        };

                                        byte[] odgovorBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(odgovor) + "\n");

                                        klijent.Send(odgovorBytes);
                                    }

                                    PrintajOsoblje(osoblje);
                                }

                                else if (tip == "update_stavka")
                                {
                                    Poruka poruka = JsonSerializer.Deserialize<Poruka>(json);

                                    foreach (Sto sto in stolovi)
                                    {
                                        Stavka updatedStavka = sto.porudzbine.Find(p => p.ID == poruka.stavkaID);
                                        if (updatedStavka == null) continue;

                                        Osoblje radnik = osoblje.Find(o => o.tcpKonekcioniSocket == klijent);

                                        if (radnik == null)
                                        {
                                            Console.WriteLine("Greska! Konekcija sa radnikom nije pronadjena.");
                                            break;
                                        }

                                        updatedStavka.status = (int)poruka.stavkaUpdate;

                                        if ((int)poruka.stavkaUpdate == 3)
                                        {
                                            notifikacijaPorudzbine.Add(updatedStavka);
                                            radnik.zauzet = false;
                                        }
                                        else if ((int)poruka.stavkaUpdate == 2)
                                        {
                                            radnik.zauzet = true;
                                        }

                                        Poruka odgovor;

                                        if (radnik.tip == "kuvar")
                                        {
                                            stackHrana.RemoveAll(s => s.ID == poruka.stavkaID);
                                            odgovor = new Poruka
                                            {
                                                tip = "stack_update",
                                                stackStavki = stackHrana
                                            };
                                        }
                                        else
                                        {
                                            stackPice.RemoveAll(s => s.ID == poruka.stavkaID);
                                            odgovor = new Poruka
                                            {
                                                tip = "stack_update_barmen",
                                                stackStavki = stackPice
                                            };
                                        }

                                        byte[] odgovorBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(odgovor) + "\n");

                                        foreach(Osoblje r in osoblje)
                                        {
                                            if (r.tip == "kuvar" || r.tip == "barmen") r.tcpKonekcioniSocket.Send(odgovorBytes);
                                        }
                                    }

                                    PrintajOsoblje(osoblje);
                                    PrintajPorudzbine(stolovi);
                                }

                                // MENADZERI
                                else if (tip == "izlistaj_rezervacije")
                                {
                                    Poruka poruka = JsonSerializer.Deserialize<Poruka>(json);

                                    Poruka odgovor = new Poruka
                                    {
                                        tip = "izlistaj_rezervacije",
                                        rezervacije = rezervacije,
                                    };

                                    byte[] odgovorBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(odgovor));
                                    klijent.Send(odgovorBytes);
                                }
                            }

                            
                        }
                        catch (SocketException)
                        {
                            // Nesto nije u redu — zatvaramo konekciju
                            klijent.Close();
                            tcpKlijenti.RemoveAt(i);
                        }

                        if(rezervacije.Count != 0)
                        {
                            List<Rezervacija> istekleRezervacije = new List<Rezervacija>();
                            foreach(Rezervacija rezervacija in rezervacije)
                            {
                                if(rezervacija.rezervacijaVreme < DateTime.Now)
                                {
                                    stolovi.RemoveAll(s => s.broj == rezervacija.brojStola);

                                    notifikacijaRezervacije.Add(rezervacija);
                                    istekleRezervacije.Add(rezervacija);
                                    
                                }
                            }

                            foreach(Rezervacija istekla in istekleRezervacije)
                            {
                                rezervacije.Remove(istekla);
                            }
                        }
                    }
                }
                // Kratka pauza u glavnoj petlji
                Thread.Sleep(100);
            }
        }

        static void PrintajOsoblje(List<Osoblje> osoblje)
        {
            Console.WriteLine("Trenutno stanje osoblja:\n");
            Console.WriteLine("========================\n");
            foreach (Osoblje radnik in osoblje)
            {
                Console.WriteLine($"Klijent: {radnik.tcpKonekcioniSocket.RemoteEndPoint}");
                Console.WriteLine($"Tip: {radnik.tip}");
                Console.WriteLine($"Status: {(radnik.zauzet ? "Zauzet" : "Nije Zauzet")}\n");
            }
            Console.WriteLine("========================\n");
        }

        static void PrintajPorudzbine(List<Sto> stolovi)
        {
            Console.WriteLine("\nTrenutno stanje porudzbina:\n");
            Console.WriteLine("========================\n");
            foreach (Sto sto in stolovi)
            {
                foreach (Stavka stavka in sto.porudzbine)
                {
                    string status;
                    if (stavka.status == 1)
                    {
                        status = "Nije Zapoceta";
                    }
                    else if (stavka.status == 2)
                    {
                        status = "Sprema Se";
                    }
                    else
                    {
                        status = "Gotova Je";
                    }

                    Console.WriteLine($"Naziv: {stavka.naziv}, x{stavka.kolicina}, Status: {status}");
                }
            }
            Console.WriteLine("\n========================\n");
        }

        static void printajStolove(List<Sto> stolovi)
        {
            Console.WriteLine("Trenutno stanje stolova:\n");
            Console.WriteLine("\n========================\n");
            foreach (Sto s in stolovi)
            {
                Console.WriteLine($"Broj Stola: {s.broj}, Broj Gostiju: {s.brojGostiju}, Stanje: {s.status}");
            }
            Console.WriteLine("\n========================");
        }
    }
}
