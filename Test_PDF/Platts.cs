using iTextSharp.text.pdf.parser;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Test_PDF
{
    public static class Platts
    {
        static string dataSource = "Platts";
        static string volume = "1";
        static string UOM = "т";

        public static List<Dictionary<string, string>> getTable(string pageText, string tableCultureName)
        {
            var records = new List<Dictionary<string, string>>();
            string currentDate = string.Empty;
            string[] pageLines = pageText.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            List<string> lastLine = new List<string>();
            List<string> regions = new List<string>() { "Asia Pacific", "Black Sea & Europe", "Black Sea and Europe", "Canada", "United States", "Latin America", "Black Sea", "Latin America*" , "Southeast Asia" };
            string currentRegion = "";

            foreach (var line in pageLines)
            {
                if (line.Contains("Unit Symbol Value Change"))
                {
                    string dateWithYear = $"{line.Substring(0, line.IndexOf("Unit")).Trim()} {DateTime.Now.Year}";
                    DateTime parsedDate = DateTime.ParseExact(dateWithYear, "MMMM d yyyy", CultureInfo.InvariantCulture);
                    if (parsedDate > DateTime.Now)
                    {
                        parsedDate = parsedDate.AddYears(-1);
                    }
                    currentDate = parsedDate.ToString("dd.MM.yyyy");
                    Table.tableHeaders = new List<string>() { line.Substring(0, line.IndexOf("Unit")).Trim(), "Region", "Unit", "Symbol", "Value", "Change" };
                }
                else
                {
                    if (regions.Contains(line))
                    {
                        if (lastLine.Count > 0)
                        {
                            Table.addRow(lastLine);
                            lastLine.Clear();
                        }
                        currentRegion = line;
                    }
                    else if (line.Contains("days") || line.Contains("weekly"))
                    {
                        lastLine[0] = lastLine[0] + " " + line;
                        Table.addRow(lastLine);
                        lastLine.Clear();
                    }
                    else
                    {
                        if (lastLine.Count > 0)
                        {
                            Table.addRow(lastLine);
                            lastLine.Clear();
                        }
                        bool find = false;
                        if (tableCultureName.Contains("|"))
                        {
                            foreach(var tableCulture in tableCultureName.Split('|'))
                                if (line.Contains(tableCulture))
                                    find = true;
                        }
                        else
                        {
                            if (line.Contains(tableCultureName))
                                find = true;
                        }
                        if (find)
                        {
                            string tempLine = line;
                            tempLine = tempLine.Trim().Replace("  ", " ");
                            string[] lineParts = tempLine.Split(' ');
                            for (int i = 0; i < 4; i++)
                            {
                                tempLine = tempLine.Substring(0, tempLine.LastIndexOf(" "));
                            }
                            lastLine = new List<string>() { tempLine, currentRegion, lineParts[lineParts.Length - 4], lineParts[lineParts.Length - 3], lineParts[lineParts.Length - 2], lineParts[lineParts.Length - 1] };
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }

            if (lastLine.Count > 0)
            {
                Table.addRow(lastLine);
                lastLine.Clear();
            }

            foreach (var row in Table.tableRows)
            {
                JsonRow jsonData = new JsonRow();
                jsonData.Data["Название товара"] = row.rowValues[0];
                jsonData.Data["Базис поставки"] = row.rowValues[0];
                jsonData.Data["Регион продажи"] = row.rowValues[1];
                jsonData.Data["Валюта цены"] = row.rowValues[2];
                jsonData.Data["Цена предложения, вал.|т"] = row.rowValues[4];
                jsonData.Data["Дата (дд.ММ.ГГ)"] = currentDate;
                jsonData.Data["Источник данных"] = dataSource;
                jsonData.Data["Объем (приведенные к т.)"] = volume;
                jsonData.Data["Единица измерения товара (стандартные сокращения)"] = UOM;
                records.Add(jsonData.Data);
            }
            return records;
        }

        public static List<Dictionary<string, string>> getTableOil(string pageText)
        {
            var records = new List<Dictionary<string, string>>();
            string currentDate = string.Empty;
            string[] pageLinesStart = pageText.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            List<string> pageLines = new List<string>();
            DateTime dateTime = DateTime.Now;
            List<string> otherCultures = new List<string>() { "Wheat", "Barley", "Corn", "Soybeans", "Soybean Oil", "Palm Oil", "DDGS", "Soybean Meal", "Exclusions" };

            foreach (var pageline in pageLinesStart)
            {
                DateTime date;
                string pageLineNew = pageline.Trim();
                bool ok = DateTime.TryParseExact(
                    pageLineNew,
                    "MMMM dd, yyyy",                  // формат
                    CultureInfo.InvariantCulture,     // английские месяцы
                    DateTimeStyles.None,
                    out date
                );
                if (otherCultures.Contains(pageLineNew))
                {
                    break;
                }
                if (pageLineNew.StartsWith("FOB-6 ports"))
                {
                    pageLines.Add(pageLineNew);
                }
                if (ok)
                {
                    dateTime = date;
                }
            }
            var mapping = new (string keyword, string type)[]
            {
                ("Offer", "Продажа"),
                ("Bid",   "Покупка"),
                ("Trade", "Продажа")
            };
            string dealType = string.Empty;
            List<string> filtered = new List<string>();

            foreach (var (keyword, type) in mapping)
            {
                List<string> filteredLines = pageLines.Where(x => x.Contains(keyword)).ToList();
                if (filteredLines.Any())
                {
                    dealType = type;
                    filtered = filteredLines.ToList();
                    break;
                }
            }
            Table.tableHeaders = new List<string>() { "Value",};
            if (!string.IsNullOrEmpty(dealType))
            {
                Regex PriceAfterKeyword = new Regex(
                    @"\b(?:Offer|Bid|Trade)\b[^\d]{0,20}(\d{1,6})(?!\d)",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
                foreach (var line in filtered)
                {
                    int price = 0;

                    var m = PriceAfterKeyword.Match(line);
                    if (m.Success)
                    {
                        List<string> currentLine = new List<string>();
                        currentLine.Add(m.Groups[1].Value);
                        Table.addRow(currentLine);
                    }
                }
            }
            foreach (var row in Table.tableRows)
            {
                JsonRow jsonData = new JsonRow();
                jsonData.Data["Источник данных"] = dataSource;
                jsonData.Data["Дата (дд.ММ.ГГ)"] = dateTime.ToString("dd.MM.yyyy");
                jsonData.Data["Название товара"] = "Подсолнечное масло";
                jsonData.Data["Базис поставки"] = "FOB";
                jsonData.Data["Валюта цены"] = "$";
                jsonData.Data["Покупка|продажа"] = dealType;
                jsonData.Data["Цена предложения, вал.|т"] = row.rowValues[0];
                jsonData.Data["Объем (приведенные к т.)"] = volume;
                jsonData.Data["Единица измерения товара (стандартные сокращения)"] = UOM;
                records.Add(jsonData.Data);
            }
            return records;
        }
    }
}
