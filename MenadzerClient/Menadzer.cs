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
        public int? brojGostiju { get; set; }
        public string? poruka { get; set; }
        public Rezervacija? rezervacija { get; set; }
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
        public List<Rezervacija>? rezervacije { get; set; }
    }

    public class Rezervacija
    {
        public int rezervacijaID { get; set; }
        public DateTime rezervacijaVreme { get; set; }
        public int brojStola { get; set; }
        public int brojGostiju { get; set; }
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
                Console.WriteLine("\n=== MENADZER MENI ===");
                Console.WriteLine("1. Kreiraj rezervaciju (UDP)");
                Console.WriteLine("2. Azuriraj rezervaciju (UDP)");
                Console.WriteLine("3. Izlistaj rezervacije (TCP)");
                Console.WriteLine("0. Izlaz");
                Console.Write("Izbor: ");
                string izbor = Console.ReadLine();

                switch (izbor)
                {
                    case "1":
                        KreirajRezervaciju(udpSocket, udpEndpoint);
                        break;

                    case "2":
                        AzurirajRezervaciju(udpSocket, udpEndpoint);
                        break;

                    case "3":
                        IzlistajRezervacije();
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

        static void KreirajRezervaciju(Socket udpSocket, EndPoint udpEndpoint)
        {
            Console.Write("Unesi broj stola: ");
            int sto = int.Parse(Console.ReadLine());

            Console.Write("Unesi broj gostiju za sto: ");
            int brojGostiju = int.Parse(Console.ReadLine());

            Console.Write("Unesi vreme rezervacije (dd.MM.yyyy HH:mm): ");
            string unosPocetak = Console.ReadLine();

            var format = "dd.MM.yyyy HH:mm";
            var kultura = System.Globalization.CultureInfo.InvariantCulture;

            DateTime vremeRezervacije = DateTime.ParseExact(unosPocetak, format, kultura);

            Rezervacija rezervacija = new Rezervacija { rezervacijaID = new Random().Next(1, 10000), rezervacijaVreme = vremeRezervacije, brojStola = sto, brojGostiju = brojGostiju };

            Poruka poruka = new Poruka
            {
                tip = "kreiraj_rezervaciju",
                sto = sto,
                brojGostiju = brojGostiju,
                rezervacija = rezervacija
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

        static void AzurirajRezervaciju(Socket udpSocket, EndPoint udpEndpoint)
        {
            Console.Write("Unesi ID rezervacije koju zelis da azuriras: ");
            int rezervacijaID = int.Parse(Console.ReadLine());

            Console.Write("Unesi azurirani broj stola: ");
            int sto = int.Parse(Console.ReadLine());

            Console.Write("Unesi azurirani broj gostiju za sto: ");
            int brojGostiju = int.Parse(Console.ReadLine());

            Console.Write("Unesi azurirano vreme rezervacije (dd.MM.yyyy HH:mm): ");
            string unosPocetak = Console.ReadLine();

            var format = "dd.MM.yyyy HH:mm";
            var kultura = System.Globalization.CultureInfo.InvariantCulture;

            DateTime vremeRezervacije = DateTime.ParseExact(unosPocetak, format, kultura);

            Rezervacija rezervacija = new Rezervacija { rezervacijaID = rezervacijaID, rezervacijaVreme = vremeRezervacije, brojStola = sto, brojGostiju = brojGostiju };

            Poruka poruka = new Poruka
            {
                tip = "azuriraj_rezervaciju",
                sto = -1,
                rezervacija = rezervacija
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

        static void IzlistajRezervacije()
        {

            Poruka poruka = new Poruka
            {
                tip = "izlistaj_rezervacije",
                sto = -1,
            };

            Odgovor odgovor = PosaljiTcpPorukuSaOdgovorom(poruka);

            if (odgovor.tip == "izlistaj_rezervacije")
            {
                Console.WriteLine("\nLista rezervacija:\n");
                Console.WriteLine("========================\n");
                foreach(Rezervacija rezervacija in odgovor.rezervacije)
                {
                    double minutaDoIsteka = 0;
                    string statusRezervacije;
                    DateTime timeNow = DateTime.Now;

                    if(rezervacija.rezervacijaVreme <= timeNow)
                    {
                        statusRezervacije = "Istekla";
                        Console.WriteLine($"Stanje rezervacije: {statusRezervacije}");
                    }
                    else
                    {
                        statusRezervacije = "Nije isetkla";
                        TimeSpan razlika = rezervacija.rezervacijaVreme - timeNow;
                        minutaDoIsteka = razlika.TotalMinutes;
                        Console.WriteLine($"Stanje rezervacije: {statusRezervacije}, Vreme do isteka u minutama: {Math.Round(minutaDoIsteka)}");
                    }
                    Console.WriteLine($"ID: {rezervacija.rezervacijaID}, Vreme: {rezervacija.rezervacijaVreme}, Broj Stola: {rezervacija.brojStola}, Broj Gostiju: {rezervacija.brojGostiju}");
                    
                }
                Console.WriteLine("\n========================\n");
            }
            else
            {
                Console.WriteLine("Unknown error");
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

            if (odgovor.notifikacijaGotovePorudzbine != null && odgovor.notifikacijaGotovePorudzbine.Count != 0)
            {
                Console.WriteLine("\nSledece porudzbine su upravo zavrsene:");
                Console.WriteLine("==============================================\n");
                foreach (Stavka porudzbina in odgovor.notifikacijaGotovePorudzbine)
                {
                    Console.WriteLine($"Naziv: {porudzbina.naziv}, x{porudzbina.kolicina}, Cena: {porudzbina.cena}, Status: {porudzbina.status}");
                }
                Console.WriteLine("\n==============================================\n");
            }

            return odgovor;
        }
    }
}