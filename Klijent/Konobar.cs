using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace KonobarClient
{
    public class Stavka
    {
        public int ID { get; set; }
        public string naziv { get; set; }
        public int kolicina { get; set; }
        public int tip { get; set; } // hrana ili pice
        public double cena { get; set; } // cena po komadu, u dinarima
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
        public List<Stavka>? stackStavki { get; set; }
        public int? stavkaID { get; set; }
        public int? stavkaUpdate { get; set; }
    }

    public class Sto
    {
        public int broj { get; set; } // broj stola
        public int brojGostiju { get; set; }
        public bool zauzet { get; set; } = false;
        public List<Stavka> porudzbine { get; set; } = new List<Stavka>();
    }

    public class Odgovor
    {
        public string tip { get; set; }
        public List<Sto>? stolovi { get; set; }
        public List<Stavka>? porudzbine { get; set; }
        public string poruka { get; set; }
        public List<Stavka>? notifikacijaGotovePorudzbine { get; set; }
    }

    internal class Konobar
    {
        static void Main(string[] args)
        {

            // UDP socket se kreira jednom (koristi se više puta)
            Socket udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            EndPoint udpEndpoint = new IPEndPoint(IPAddress.Loopback, 15000);

            int stavkaId = 1;

            while (true)
            {
                Console.WriteLine("\n=== KONOBAR MENI ===");
                Console.WriteLine("1. Prijavi dolazak gostiju (UDP)");
                Console.WriteLine("2. Pošalji porudžbinu (TCP)");
                Console.WriteLine("3. Zatraži račun (TCP)");
                Console.WriteLine("4. Proveri koje su porudzbine gotove (TCP)");
                Console.WriteLine("5. Stanje restorana (TCP)");
                Console.WriteLine("0. Izlaz");
                Console.Write("Izbor: ");
                string izbor = Console.ReadLine();

                switch (izbor)
                {
                    case "1":
                        PrijaviDolazakGostiju(udpSocket, udpEndpoint);
                        break;

                    case "2":
                        PosaljiPorudzbinu();
                        break;

                    case "3":
                        ZahtevajRacun();
                        break;

                    case "4":
                        ProveriPorudzbine();
                        break;

                    case "5":
                        StanjeRestorana();
                        break;

                    case "0":
                        udpSocket.Close();
                        Console.WriteLine("Konobar zatvara aplikaciju...");
                        return;

                    default:
                        Console.WriteLine("Nepoznata opcija.");
                        break;
                }
            }
        }

        static void PrijaviDolazakGostiju(Socket udpSocket, EndPoint udpEndpoint)
        {
            Console.Write("Unesi broj stola: ");
            int sto = int.Parse(Console.ReadLine());

            Console.Write("Unesi broj gostiju za sto: ");
            int brojGostiju = int.Parse(Console.ReadLine());


            var poruka = new Poruka
            {
                tip = "dolazak",
                sto = sto,
                brojGostiju = brojGostiju
            };

            // JsonSerializer jer iskace da je BinaryFormatter obsolete i iskacu errori
            string json = JsonSerializer.Serialize(poruka);
            byte[] buffer = Encoding.UTF8.GetBytes(json);

            udpSocket.SendTo(buffer, udpEndpoint);
            byte[] prijemniBuffer = new byte[1024];
            EndPoint serverEndpoint = new IPEndPoint(IPAddress.Any, 0);
            int brPrimljenih = udpSocket.ReceiveFrom(prijemniBuffer, ref serverEndpoint);

            string odgovor = Encoding.UTF8.GetString(prijemniBuffer, 0, brPrimljenih);
            Console.WriteLine($"Server kaže: {odgovor}");
        }

        static void PosaljiPorudzbinu()
        {
            Console.Write("Unesi broj stola: ");
            int sto = int.Parse(Console.ReadLine());

            Console.WriteLine("Unosite stavke porudžbine. Prazan unos za završava unos.\n");

            // lista stavki
            List<Stavka> stavke = new List<Stavka>();

            while (true)
            {
                Console.Write("Naziv jela/pića: ");
                string naziv = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(naziv)) break;

                Console.Write("Tip stavke (1 - Hrana, 2 - Pice): ");
                int tip = int.Parse(Console.ReadLine());

                if(tip != 1 && tip != 2)
                {
                    Console.WriteLine("Unos pogresan, pokusajte ponovo");
                    continue;
                }
                
                Console.Write("Broj porcija: ");
                int kolicina = int.Parse(Console.ReadLine());

                Console.Write("Cena po porciji (RSD): ");
                double cena = double.Parse(Console.ReadLine());

                stavke.Add(new Stavka
                {
                    ID = new Random().Next(1, 10000),
                    naziv = naziv,
                    tip = tip,
                    kolicina = kolicina,
                    cena = cena,
                    status = 1
                });

                Console.WriteLine("Dodato!\n");
            }

            Poruka poruka = new Poruka
            {
                tip = "porudzbina",
                sto = sto,
                stavke = stavke
            };

            Console.WriteLine("Pokusaj dodavanja sledecih stavki:");
            foreach(var stavka in stavke)
            {
                Console.WriteLine($"Naziv: {stavka.naziv}, Kolicina: {stavka.kolicina}, Cena(kom): {stavka.cena}");
            }

            Odgovor odgovor = PosaljiTcpPorukuSaOdgovorom(poruka);

            if (odgovor.tip == "potvrda")
            {
                Console.WriteLine("Porudžbina uspešno primljena!");
            }
            else if (odgovor.tip == "greska")
            {
                Console.WriteLine($"Greška: {odgovor.poruka}");
            }
            else
            {
                Console.WriteLine("Unknown error");
            }
        }

        static void ZahtevajRacun()
        {
            Console.Write("Unesi broj stola: ");
            int sto = int.Parse(Console.ReadLine());

            Poruka poruka = new Poruka
            {
                tip = "racun",
                sto = sto
            };

            Odgovor odgovor = PosaljiTcpPorukuSaOdgovorom(poruka);

            if (odgovor.tip == "potvrda")
            {
                Console.WriteLine(odgovor.poruka);
            }
            else if (odgovor.tip == "greska")
            {
                Console.WriteLine($"Greška: {odgovor.poruka}");
                return;
            }
            else
            {
                Console.WriteLine("Unknown error");
                return;
            }

            Console.Write("Unesi uplacenu sumu: ");
            double uplata = double.Parse(Console.ReadLine());

            poruka = new Poruka
            {
                tip = "uplata",
                sto = sto,
                uplata = uplata
            };

            odgovor = PosaljiTcpPorukuSaOdgovorom(poruka);

            if (odgovor.tip == "potvrda")
            {
                Console.WriteLine(odgovor.poruka);
                Console.WriteLine($"Sto {sto} vise nije zauzet");
            }
            else if (odgovor.tip == "greska")
            {
                Console.WriteLine($"Greška: {odgovor.poruka}");
            }
            else
            {
                Console.WriteLine("Unknown error");
            }
        }

        static void ProveriPorudzbine()
        {
            Poruka poruka = new Poruka
            {
                tip = "stanje_porudzbina",
                sto = -1
            };

            Odgovor odgovor = PosaljiTcpPorukuSaOdgovorom(poruka);


            if(odgovor.porudzbine.Count == 0)
            {
                Console.WriteLine("Trenutno nema aktivnih porudzbina.");
                return;
            }

            if (odgovor.tip == "stanje_porudzbina")
            {
                Console.WriteLine("\nLista Porudzbina Iz Kuhinje:\n");
                foreach (Stavka porudzbina in odgovor.porudzbine)
                {
                    if(porudzbina.tip != 1)
                    {
                        continue;
                    }

                    string status;
                    if(porudzbina.status == 1) {
                        status = "Nije Zapoceta";
                    }else if (porudzbina.status == 2)
                    {
                        status = "Sprema Se";
                    }else
                    {
                        status = "Gotova Je";
                    }
                    
                    Console.WriteLine($"Naziv: {porudzbina.naziv}, x{porudzbina.kolicina}, Status: {status}");
                }

                Console.WriteLine("\nLista Porudzbina Sa Bara:\n");
                foreach (Stavka porudzbina in odgovor.porudzbine)
                {
                    if (porudzbina.tip != 2)
                    {
                        continue;
                    }

                    string status;
                    if (porudzbina.status == 1)
                    {
                        status = "Nije Zapoceta";
                    }
                    else if (porudzbina.status == 2)
                    {
                        status = "Sprema Se";
                    }
                    else
                    {
                        status = "Gotova Je";
                    }

                    Console.WriteLine($"Naziv: {porudzbina.naziv}, x{porudzbina.kolicina}, Status: {status}");
                }

            }
        }

            static void StanjeRestorana()
        {
            Poruka poruka = new Poruka
            {
                tip = "stanje_restorana",
                sto = -1
            };

            Odgovor odgovor = PosaljiTcpPorukuSaOdgovorom(poruka);

            if(odgovor.tip == "stanje_restorana")
            {
                if(odgovor.stolovi.Count == 0)
                {
                    Console.WriteLine("Trenutno nijedan sto nije zauzet.");
                }
                else
                {
                    Console.WriteLine($"Trenutno je zauzeto {odgovor.stolovi.Count} stolova:");
                    foreach (Sto sto in odgovor.stolovi)
                    {
                        string status = sto.zauzet ? "Zauzet" : "Slobodan";
                        Console.WriteLine($" - Sto {sto.broj}, broj gostiju: {sto.brojGostiju}, ukupno stavki {sto.porudzbine.Count}: ");
                        foreach(Stavka stavka in sto.porudzbine)
                        {
                            Console.WriteLine($"Naziv: {stavka.naziv}, Kolicina: {stavka.kolicina}, Cena(kom): {stavka.cena}, Status: {stavka.status}");
                        }
                    }
                }
                    
            }
        }

            static void PosaljiTcpPoruku(object poruka)
        {
            Socket tcpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            tcpSocket.Connect(new IPEndPoint(IPAddress.Loopback, 16000));

            // JsonSerializer jer iskace da je BinaryFormatter obsolete i iskacu errori
            string json = JsonSerializer.Serialize(poruka);
            byte[] buffer = Encoding.UTF8.GetBytes(json);
            tcpSocket.Send(buffer);

            tcpSocket.Shutdown(SocketShutdown.Both);
            tcpSocket.Close();
        }

        static Odgovor PosaljiTcpPorukuSaOdgovorom(object poruka)
        {
            Socket tcpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            tcpSocket.Connect(new IPEndPoint(IPAddress.Loopback, 16000));

            // JsonSerializer jer iskace da je BinaryFormatter obsolete i iskacu errori
            string json = JsonSerializer.Serialize(poruka);
            byte[] buffer = Encoding.UTF8.GetBytes(json);
            tcpSocket.Send(buffer);

            byte[] prijemniBuffer = new byte[2048];
            int primljeno = tcpSocket.Receive(prijemniBuffer);

            tcpSocket.Shutdown(SocketShutdown.Both);
            tcpSocket.Close();

            string odgovorJson = Encoding.UTF8.GetString(prijemniBuffer, 0, primljeno);
            Odgovor odgovor = JsonSerializer.Deserialize<Odgovor>(odgovorJson);

            if(odgovor.notifikacijaGotovePorudzbine != null && odgovor.notifikacijaGotovePorudzbine.Count != 0)
            {
                Console.WriteLine("\nSledece porudzbine su upravo zavrsene:");
                Console.WriteLine("==============================================\n");
                foreach(Stavka porudzbina in odgovor.notifikacijaGotovePorudzbine)
                {
                    Console.WriteLine($"Naziv: {porudzbina.naziv}, x{porudzbina.kolicina}, Cena: {porudzbina.cena}, Status: {porudzbina.status}");
                }
                Console.WriteLine("\n==============================================\n");
            }

            return odgovor;
        }
    }
}