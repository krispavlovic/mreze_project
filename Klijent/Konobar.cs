using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace KonobarClient
{
    public class Stavka
    {
        public string naziv { get; set; }
        public int kolicina { get; set; }
        public double cena { get; set; } // cena po komadu, u dinarima
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
    }

    public class OdgovorStanje
    {
        public string tip { get; set; }
        public List<Sto> stolovi { get; set; }
    }

    internal class Konobar
    {
        static void Main(string[] args)
        {

            // UDP socket se kreira jednom (koristi se više puta)
            Socket udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            EndPoint udpEndpoint = new IPEndPoint(IPAddress.Loopback, 15000);

            while (true)
            {
                Console.WriteLine("\n=== KONOBAR MENI ===");
                Console.WriteLine("1. Prijavi dolazak gostiju (UDP)");
                Console.WriteLine("2. Pošalji porudžbinu (TCP)");
                Console.WriteLine("3. Zatraži račun (TCP)");
                Console.WriteLine("4. Stanje restorana (TCP)");
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

                Console.Write("Broj porcija: ");
                int kolicina = int.Parse(Console.ReadLine());

                Console.Write("Cena po porciji (RSD): ");
                double cena = double.Parse(Console.ReadLine());

                stavke.Add(new Stavka
                {
                    naziv = naziv,
                    kolicina = kolicina,
                    cena = cena
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

            string odgovorJson = PosaljiTcpPorukuSaOdgovorom(poruka);

            JsonElement odgovor = JsonSerializer.Deserialize<JsonElement>(odgovorJson);

            if (odgovor.GetProperty("tip").GetString() == "potvrda")
            {
                Console.WriteLine("Porudžbina uspešno primljena!");
            }
            else if (odgovor.GetProperty("tip").GetString() == "greska")
            {
                Console.WriteLine($"Greška: {odgovor.GetProperty("poruka").GetString()}");
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

            string odgovorJson = PosaljiTcpPorukuSaOdgovorom(poruka);

            JsonElement odgovor = JsonSerializer.Deserialize<JsonElement>(odgovorJson);

            if (odgovor.GetProperty("tip").GetString() == "potvrda")
            {
                Console.WriteLine(odgovor.GetProperty("poruka").GetString());
            }
            else if (odgovor.GetProperty("tip").GetString() == "greska")
            {
                Console.WriteLine($"Greška: {odgovor.GetProperty("poruka").GetString()}");
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

            odgovorJson = PosaljiTcpPorukuSaOdgovorom(poruka);

            odgovor = JsonSerializer.Deserialize<JsonElement>(odgovorJson);

            if (odgovor.GetProperty("tip").GetString() == "potvrda")
            {
                Console.WriteLine(odgovor.GetProperty("poruka").GetString());
                Console.WriteLine($"Sto {sto} vise nije zauzet");
            }
            else if (odgovor.GetProperty("tip").GetString() == "greska")
            {
                Console.WriteLine($"Greška: {odgovor.GetProperty("poruka").GetString()}");
            }
            else
            {
                Console.WriteLine("Unknown error");
            }
        }

        static void StanjeRestorana()
        {
            Poruka poruka = new Poruka
            {
                tip = "stanje_restorana",
                sto = -1
            };

            string odgovorJson = PosaljiTcpPorukuSaOdgovorom(poruka);

            // JsonSerializer jer iskace da je BinaryFormatter obsolete i iskacu errori
            OdgovorStanje odgovor = JsonSerializer.Deserialize<OdgovorStanje>(odgovorJson);

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
                        Console.WriteLine($" - Sto {sto.broj}, broj gostiju: {sto.brojGostiju}, ukupno stavki: {sto.porudzbine.Count}");
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

        static string PosaljiTcpPorukuSaOdgovorom(object poruka)
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

            return Encoding.UTF8.GetString(prijemniBuffer, 0, primljeno);
        }
    }
}