using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace KuvarClient
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
        public string? uloga { get; set; }
        public List<Stavka> stackStavki { get; set; }
        public int? stavkaID { get; set; }
        public int? stavkaUpdate { get; set; }
    }

    internal class Kuvar
    {
        static void Main(string[] args)
        {
            var tcp = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            tcp.Connect(new IPEndPoint(IPAddress.Loopback, 16000));
            tcp.Blocking = false;

            byte[] buffer = new byte[4096];
            string rxBuffer = "";
            // Prijavi se serveru kao KUVAR (trajna veza)

            Poruka poruka = new Poruka
            {
                tip = "prijava",
                sto = -1,
                uloga = "kuvar"
            };

            var prijava = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(poruka));
            tcp.Send(prijava);

            Stavka trenutnaPorudzbina = null;
            List<Stavka> trenutniStack = null;

            Console.WriteLine("Kuvar povezan. Ceka dodelu zadataka...\n");

            // Neblokirajuća petlja: Poll za prijem, plus lokalni meni komandi koje šalju TCP poruke
            while (true)
            {
                // 1) Prijem poruka sa servera (dodele zadataka, potvrde, greške...)
                if (tcp.Poll(200 * 1000, SelectMode.SelectRead)) // 0.2s
                {
                    int porukaBytes = 0;
                    try { porukaBytes = tcp.Receive(buffer); }
                    catch (SocketException) { porukaBytes = 0; }

                    if (porukaBytes <= 0)
                    {
                        Console.WriteLine("Veza sa serverom zatvorena.");
                        try { tcp.Close(); } catch { }
                        return;
                    }


                    // Dopuni buffer
                    rxBuffer += Encoding.UTF8.GetString(buffer, 0, porukaBytes);

                    // Vadi sve poruke do '\n'
                    int idx;
                    while ((idx = rxBuffer.IndexOf('\n')) >= 0)
                    {
                        string jedanJson = rxBuffer.Substring(0, idx);
                        rxBuffer = rxBuffer.Substring(idx + 1);

                        if (string.IsNullOrWhiteSpace(jedanJson)) continue;

                        try
                        {
                            Poruka data = JsonSerializer.Deserialize<Poruka>(jedanJson);
                            string tip = data.tip;

                            if (tip == "stack_update")
                            {

                                if (data.stackStavki.Count == 0)
                                {
                                    Console.WriteLine("Nema novih porudzbina za kuhinju.");
                                }
                                else
                                {
                                    if (trenutnaPorudzbina != null && data.stackStavki.Find(s => s.ID == trenutnaPorudzbina.ID) != null)
                                    {
                                        data.stackStavki.RemoveAll(s => s.ID == trenutnaPorudzbina.ID);
                                        Console.WriteLine($"Obrisan ID {trenutnaPorudzbina.ID}, {trenutnaPorudzbina.naziv}");
                                    }

                                    Console.WriteLine("Lista novih porudzbina za kuhinju:\n");
                                    foreach (Stavka stavka in data.stackStavki)
                                    {
                                        Console.WriteLine($"Naziv: {stavka.naziv}, Kolicina: {stavka.kolicina}, Cena(kom): {stavka.cena}");
                                    }
                                    if (trenutnaPorudzbina != null && trenutnaPorudzbina.status != 3) trenutniStack = data.stackStavki;
                                }

                                if (data.stackStavki.Count == 0)
                                {
                                    Console.WriteLine("Ceka se prva porudzbina...");
                                }
                                else
                                {
                                    Console.WriteLine("^ Sledeca porudzbina");
                                }

                                if ((trenutnaPorudzbina == null || trenutnaPorudzbina.status == 3) && data.stackStavki.Count != 0)
                                {
                                    while (true)
                                    {
                                        Console.WriteLine("\nKada ste spremni da preuzmete sledecu porudzbinu pritisnite 'Y'");
                                        string odgovor = Console.ReadLine();

                                        if (odgovor.ToLower() == "y")
                                        {
                                            trenutnaPorudzbina = data.stackStavki[data.stackStavki.Count - 1];
                                            data.stackStavki.RemoveAt(data.stackStavki.Count - 1);

                                            trenutniStack = data.stackStavki;

                                            Poruka updateStavka = new Poruka
                                            {
                                                tip = "update_stavka",
                                                sto = -1,
                                                stavkaID = trenutnaPorudzbina.ID,
                                                stavkaUpdate = 2
                                            };

                                            var updateStavkaBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(updateStavka));

                                            tcp.Send(updateStavkaBytes);
                                            break;
                                        }
                                        else
                                        {
                                            continue;
                                        }
                                    }

                                }
                            }
                        }
                        catch (JsonException ex)
                        {
                            Console.WriteLine("Parse error: " + ex.Message);
                            Console.WriteLine("Raw: " + jedanJson);
                        }
                    }

                    //var json = Encoding.UTF8.GetString(buffer, 0, porukaBytes);
                    //Poruka data = JsonSerializer.Deserialize<Poruka>(json);

                    
                }


                if (trenutnaPorudzbina != null && trenutnaPorudzbina.status != 3)
                {
                    Console.WriteLine("\nOpcije:");
                    Console.WriteLine("1 - Prikazi trenutnu porudzbinu");
                    Console.WriteLine("2 - Označi porudžbinu kao završenu");
                    Console.WriteLine("3 - Prikazi porudzbine koje cekaju");
                    Console.WriteLine("Enter - ništa (nastavi sačekati nove porudžbine)");

                    string akcija = Console.ReadLine();

                    if (akcija == "1")
                    {
                        Console.WriteLine($"Trenutna porudžbina: {trenutnaPorudzbina.naziv}, x{trenutnaPorudzbina.kolicina}");
                    }
                    else if (akcija == "2")
                    {
                        trenutnaPorudzbina.status = 3;
                        Console.WriteLine("Porudžbina označena kao završena!");

                        Poruka updateStavka = new Poruka
                        {
                            tip = "update_stavka",
                            sto = -1,
                            stavkaID = trenutnaPorudzbina.ID,
                            stavkaUpdate = 3
                        };

                        var updateStavkaBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(updateStavka));

                        tcp.Send(updateStavkaBytes);

                    }
                    else if (akcija == "3")
                    {
                        if (trenutniStack != null && trenutniStack.Count != 0)
                        {
                            foreach (Stavka stavka in trenutniStack)
                            {
                                Console.WriteLine($"Naziv: {stavka.naziv}, Kolicina: {stavka.kolicina}, Cena(kom): {stavka.cena}");
                            }
                        }
                    }
                }else if(trenutnaPorudzbina != null && trenutnaPorudzbina.status == 3 && trenutniStack != null && trenutniStack.Count != 0)
                {
                    if (trenutniStack != null && trenutniStack.Count != 0)
                    {
                        Console.WriteLine("\nOstale porudzbine:\n");
                        foreach (Stavka stavka in trenutniStack)
                        {
                            Console.WriteLine($"Naziv: {stavka.naziv}, Kolicina: {stavka.kolicina}, Cena(kom): {stavka.cena}");
                        }
                        Console.WriteLine("^ Sledeca porudzbina");
                    }

                    while (true)
                    {
                        Console.WriteLine("\nKada ste spremni da preuzmete sledecu porudzbinu pritisnite 'Y'");
                        string odgovor = Console.ReadLine();

                        if (odgovor.ToLower() == "y")
                        {
                            trenutnaPorudzbina = trenutniStack[trenutniStack.Count - 1];
                            trenutniStack.RemoveAt(trenutniStack.Count - 1);

                            Poruka updateStavka = new Poruka
                            {
                                tip = "update_stavka",
                                sto = -1,
                                stavkaID = trenutnaPorudzbina.ID,
                                stavkaUpdate = 2
                            };

                            var updateStavkaBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(updateStavka));

                            tcp.Send(updateStavkaBytes);
                            break;
                        }
                        else
                        {
                            continue;
                        }
                    }
                }
            }
        }

    }

}