using Org.BouncyCastle.Ocsp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Test_PDF
{
    internal class Tambov
    {

        static string dataSource = "Тамбовский бекон";
        static string buySell = "Покупка";
        static string client = "ООО «Тамбовский бекон»";
        static string volume = "1";
        static string UOM = "Т";
        static string currency = "RUB";
        static string NDS = "без НДС";
        static List<string> cultures = new List<string>() { "Пшеница", "Горох", "Кукуруза", "Подсолнечник", "Соя", "Ячмень", "Сорго", "Рапс", "Чечевица", "Нут", "Лен", "Овес" };
        

        public static Dictionary<string, List<Dictionary<string, string>>> getTable(string pageText)
        {
            var result = new Dictionary<string, List<Dictionary<string, string>>>();
            pageText = pageText.Replace("\r\n\r\n", "|");
            string[] pageLines = pageText.Split(new[] {'|' }, StringSplitOptions.RemoveEmptyEntries);
            string startDate = string.Empty;
            string endDate = string.Empty;
            string region = string.Empty;
            string contacts = string.Empty;
            List<string> quantities = new List<string>();

            bool nowTable = false;
            bool nowRegion = false;
            int currentCollumn = 1;
            List<string> currentRow = new List<string>();

            for (int i = 0; i < pageLines.Length; i++)
            {
                if (nowTable)
                {
                    if (nowRegion)
                    {
                        if (pageLines[i].Contains("№") && pageLines[i].Contains("п/п"))
                            nowRegion = false;
                        else
                        {
                            region = region + pageLines[i] + "\r\n";
                        }
                    }
                    else
                    {
                        currentRow.Add(pageLines[i].Replace("\r\n", " "));
                        currentCollumn++;
                        if (currentCollumn > 3)
                        {
                            if (Table.tableHeaders.Count == 0)
                                Table.tableHeaders = currentRow.ToArray().ToList();
                            else
                                Table.addRow(currentRow.ToArray().ToList());
                            currentRow.Clear();
                            currentCollumn = 0;
                        }
                    }
                }
                if (pageLines[i].Contains("период"))
                {
                    string tempStr = pageLines[i].Substring(pageLines[i].IndexOf("период"));
                    Regex reg = new Regex(@"\d{2}[.]\d{2}[.]\d{2}");
                    MatchCollection ma = reg.Matches(tempStr);
                    if (ma.Count == 2)
                    {
                        startDate = ma[0].Value;
                        endDate = ma[1].Value;
                    }
                }

                if (pageLines[i].Contains("Адрес поставки:"))
                {
                    region = pageLines[i].Substring(pageLines[i].IndexOf("Адрес поставки:") + "Адрес поставки:".Length).Trim();
                    region = region.Replace("№", "").Replace("п/п", "").Trim();
                    if (region == "")
                        nowRegion = true;
                    currentRow.Add("№ п/п");
                    nowTable = true;
                }
                if (pageLines[i].Contains("Контактные лица:"))
                {
                    contacts = pageLines[i].Substring(pageLines[i].IndexOf("Контактные лица:") + "Контактные лица:".Length).Trim();
                    contacts = contacts.Replace("\r\n\r\n", "\n");
                    string[] tempContacts = contacts.Split('\n');
                    contacts = string.Empty;
                    foreach(var contact in tempContacts)
                    {
                        if (contact.IndexOf("телефон:") < 0)
                        {
                            Regex reg = new Regex(@"\d-\d{3}-\d{3}-\d{2}-\d{2}");
                            Match match = reg.Match(contact);
                            contacts = contacts + "\n" + contact.Substring(match.Index);
                        }
                        else
                            contacts = contacts + "\n" + contact.Substring(contact.IndexOf("телефон:"));
                    }
                    contacts = contacts.Trim();
                    break;
                }

                if (pageLines[i].StartsWith("*"))
                {
                    string[] lineParts = pageLines[i].Replace("\r\n\r\n", "|").Split('|');
                    foreach(var part in lineParts)
                    {
                        string tempStr = part.Replace("*", "").Trim();
                        if (tempStr.Contains("скидка"))
                            continue;
                        tempStr = tempStr.Replace("Для Пшеницы кормовой", "Пшеница кормовая");
                        tempStr = tempStr.Replace("для всех зерновых ", "");
                        quantities.Add(tempStr);
                        nowTable = false;
                    }
                }
            }
            foreach(string culture in cultures)
            {
                var records = new List<Dictionary<string, string>>();
                foreach (var row in Table.tableRows)
                {
                    string currentCulture = getCulture(row.rowValues[1].Trim());
                    if (currentCulture == culture && row.rowValues[2].Trim() != "не покупаем")
                    {
                        JsonRow jsonData = new JsonRow();
                        string firstWord = row.rowValues[1].Substring(0, row.rowValues[1].IndexOf(" "));
                        string currentQuantity = "";
                        foreach (var quantity in quantities)
                        {
                            if (quantity.StartsWith(firstWord))
                            {
                                if (firstWord == "Пшеница")
                                {
                                    Regex reg = new Regex(@"\d{1,2}[,]{0,1}\d{0,1}%");
                                    Match match = reg.Match(row.rowValues[1]);
                                    string currentProc = match.Value;
                                    match = reg.Match(quantity);
                                    if (match.Value == currentProc)
                                    {
                                        currentQuantity = quantity;
                                        break;
                                    }
                                    else
                                    {
                                        continue;
                                    }
                                }
                                else
                                {
                                    currentQuantity = quantity;
                                }
                                break;
                            }
                        }
                        jsonData.Data["Дата (дд.ММ.ГГ)"] = startDate;
                        jsonData.Data["Дата окончания (дд.ММ.ГГ)"] = endDate;
                        jsonData.Data["Источник данных"] = dataSource;
                        jsonData.Data["Покупка|продажа"] = buySell;
                        jsonData.Data["Клиент (Название)"] = client;
                        jsonData.Data["Клиент (Контакты)"] = contacts;
                        jsonData.Data["Регион продажи"] = region;
                        jsonData.Data["Название товара"] = row.rowValues[1].Trim();
                        jsonData.Data["Качество"] = currentQuantity;
                        jsonData.Data["Валюта цены"] = currency;

                        jsonData.Data["Объем (приведенные к т.)"] = volume;
                        jsonData.Data["Единица измерения товара (стандартные сокращения)"] = UOM;
                        jsonData.Data["Цена предложения, вал.|т"] = row.rowValues[3];
                        jsonData.Data["Признак с НДС|без НДС"] = NDS;
                        jsonData.Data["Цена, руб|т без НДС"] = row.rowValues[3];
                        records.Add(jsonData.Data);
                    }
                }
                if (records.Count > 0)
                {
                    result.Add(culture, records);
                }
            }

            return result;
        }

        static string getCulture(string culture)
        {
            string tempCulture = culture.Replace("ё", "е");
            foreach (var cult in cultures)
            {
                if (tempCulture.Contains(cult))
                {
                    return cult;
                }
            }
            return string.Empty;
        }
    }
}
