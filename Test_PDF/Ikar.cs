using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using static System.Net.Mime.MediaTypeNames;
using System.Globalization;


namespace Test_PDF
{
    internal class Ikar
    {
        static string dataSource = "Икар";
        static string volume = "1";
        static string UOM = "тн";
        static string NDS = "c НДС";
        public static Dictionary<string, List<Dictionary<string, string>>> getTable4(string pageText)
        {
            var result = new Dictionary<string, List<Dictionary<string, string>>>();
            string[] pageLines = pageText.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            Regex reg = new Regex(@"\d");
            string startDate = string.Empty;
            string partString = string.Empty;
            string region = "";
            List<string> cultures = new List<string>() { "Подсолнечное масло нераф", "Соевое масло", "Рапсовое масло", "Подсолнечный шрот", "Соевый шрот", "Рапсовый шрот" };
            List<string> regions = new List<string>() { "ЦФО", "Порт РФ", "ЮФО", "РФ", "ПФО" };

            for (int i = 0; i < pageLines.Length; i++)
            {
                Match match = reg.Match(pageLines[i]);
                if (match.Success || startDate != string.Empty)
                {
                    if ((pageLines[i].Contains("за") || pageLines[i].Contains("неделю")) && pageLines[i].Length < 20)
                        continue;
                    if (pageLines[i].Length < 20)
                    {
                        partString = pageLines[i];
                        continue;
                    }
                    else
                    {
                        pageLines[i] = partString + " " + pageLines[i];
                        partString = string.Empty;
                    }
                    reg = new Regex(@"\d{1,2}[.]\d{1,2}[.]2\d");
                    match = reg.Match(pageLines[i]);
                    if (match.Success)
                    {
                        startDate = match.Value;
                        DateTime date = DateTime.ParseExact(startDate, "d.M.yy", CultureInfo.InvariantCulture);
                        int dayOfWeek = (int)date.DayOfWeek;
                        if (dayOfWeek == 0) dayOfWeek = 7;
                        int daysToFriday = 5 - dayOfWeek;
                        DateTime fridayDate = date.AddDays(daysToFriday);
                        startDate = fridayDate.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
                        Table.tableHeaders = new List<string>() { "Регион", "Цена", "Культура" };
                    }
                    else
                    {
                        string tempStr = pageLines[i].Trim();
                        int spaceCount = 0;
                        for (int j = 0; j < tempStr.Length; j++)
                        {
                            if (tempStr[j] == ' ') 
                                spaceCount++;
                        }
                        if (Regex.Match(tempStr, @"\d").Success && spaceCount > 4)
                        {
                            tempStr = tempStr.Substring(0, tempStr.LastIndexOf(' '));
                            tempStr = tempStr.Substring(0, tempStr.LastIndexOf(' '));
                            string sum3 = tempStr.Substring(tempStr.LastIndexOf(" "));
                            tempStr = tempStr.Substring(0, tempStr.LastIndexOf(' '));
                            string sum2 = tempStr.Substring(tempStr.LastIndexOf(" "));
                            tempStr = tempStr.Substring(0, tempStr.LastIndexOf(' '));
                            string sum1 = tempStr.Substring(tempStr.LastIndexOf(" "));
                            tempStr = tempStr.Substring(0, tempStr.LastIndexOf(' '));
                            region = region + " " + tempStr;
                            string culture = "";

                            foreach(var currentCulture in cultures)
                            {
                                if (region.Contains(currentCulture))
                                {
                                    culture = currentCulture;
                                    break;
                                }
                            }

                            foreach(var currentRegion in regions)
                            {
                                if (region.ToLower().Contains(currentRegion.ToLower()))
                                { 
                                    region = currentRegion;
                                    break;
                                }
                            }
                            if (culture != "" && region != "")
                            {
                                Table.addRow(new List<string> { region, sum1, culture });
                            }
                            region = "";
                        }
                        else
                        {
                            region = tempStr;
                        }
                    }
                }
            }

            foreach (var culture in cultures)
            {
                var records = new List<Dictionary<string, string>>();
                foreach (var row in Table.tableRows)
                {
                    if (row.rowValues[2].Trim() == culture)
                    {
                        JsonRow jsonData = new JsonRow();

                        jsonData.Data["Дата (дд.ММ.ГГ)"] = startDate;
                        jsonData.Data["Источник данных"] = dataSource;
                        jsonData.Data["Покупка|продажа"] = "Продажа";
                        jsonData.Data["Регион"] = row.rowValues[0].Trim();
                        jsonData.Data["Регион продажи"] = row.rowValues[0].Trim();
                        jsonData.Data["Название товара"] = row.rowValues[2].Replace("нераф", "").Trim();
                        if (row.rowValues[0].Trim().ToLower().Contains("порт"))
                            jsonData.Data["Базис поставки"] = "СРТ";
                        else
                            jsonData.Data["Базис поставки"] = "EXW";
                        jsonData.Data["Объем (приведенные к т.)"] = volume;
                        jsonData.Data["Единица измерения товара (стандартные сокращения)"] = UOM;
                        jsonData.Data["Признак с НДС|без НДС"] = "с НДС";
                        jsonData.Data["Цена предложения, вал.|т"] = row.rowValues[1].Trim();
                        jsonData.Data["Цена, руб|т без НДС"] = row.rowValues[1].Trim();
                        records.Add(jsonData.Data);
                    }
                }
                if (records.Count > 0)
                    result.Add(culture.Replace("нераф", "").Trim(), records);
            }
            
            return result;
        }
        public static Dictionary<string, List<Dictionary<string, string>>> getTable3(string pageText, string currentDate)
        {
            var result = new Dictionary<string, List<Dictionary<string, string>>>();

            var monthMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                { "января", 1 }, { "февраля", 2 }, { "марта", 3 }, { "апреля", 4 },
                { "мая", 5 }, { "июня", 6 }, { "июля", 7 }, { "августа", 8 },
                { "сентября", 9 }, { "октября", 10 }, { "ноября", 11 }, { "декабря", 12 }
            };
            string pattern = @"\b(\d{1,2})\s+([а-яА-Я]+)(?:\s+(\d{4}))?\b";
            var match = Regex.Match(currentDate, pattern);
            if (!int.TryParse(match.Groups[1].Value, out int day) || day < 1 || day > 31)
            {
                return result;
            }
            string monthName = match.Groups[2].Value;
            if (!monthMap.TryGetValue(monthName, out int month))
            {
                return result;
            }
            int year;
            DateTime now = DateTime.Now;
            if (match.Groups[3].Success && int.TryParse(match.Groups[3].Value, out int parsedYear))
            {
                year = parsedYear;
            }
            else
            {
                year = now.Year;
                DateTime parsedDate = new DateTime(year, month, day);
                if (parsedDate > now)
                {
                    year--;
                    parsedDate = new DateTime(year, month, day);
                }
            }
            DateTime date = new DateTime(year, month, day);
            int dayOfWeek = (int)date.DayOfWeek;
            if (dayOfWeek == 0) dayOfWeek = 7;
            int daysToFriday = 5 - dayOfWeek;
            DateTime fridayDate = date.AddDays(daysToFriday);
            string startDate = fridayDate.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

            string[] pageLines = pageText.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            Regex reg = new Regex(@"\b\d+,\d+\b|\b\d+\b");
            string partString = string.Empty;

            Table.tableHeaders = new List<string>() { "Регион", "Цена" };

            List<string> regions = new List<string>() { "Воронежская область", "Тамбовская область", "Липецкая область", "Пензенская область", "Самарская область", "Саратовская область",
            "Краснодарский край", "Ставропольский край", "Ростовская область", "Средняя по РФ"};

            for (int i = 0; i < pageLines.Length; i++)
            {
                match = reg.Match(pageLines[i]);
                if (match.Success)
                {
                    var matches = reg.Matches(pageLines[i]);
                    string lastMatch = matches[matches.Count - 1].Value;

                    pattern = @"\b(\d+,\d+|\d+|[ХX]|нет\s+цены)\b";
                    match = Regex.Match(pageLines[i], pattern);
                    string text = pageLines[i].Substring(0, match.Index).Trim();
                    if (text.Contains("/"))
                        text.Substring(0, text.IndexOf("/")).Trim();

                    foreach(var region in regions)
                    {
                        if (text.Contains(region))
                        {
                            if (decimal.TryParse(lastMatch, System.Globalization.NumberStyles.Any, new System.Globalization.CultureInfo("ru-RU"), out decimal number))
                            {
                                Table.addRow(new List<string> { region, ((long)number * 1000).ToString() });
                            }
                            break;
                        }
                    }

                }
            }
            var records = new List<Dictionary<string, string>>();
            foreach (var row in Table.tableRows)
            {
                JsonRow jsonData = new JsonRow();

                jsonData.Data["Дата (дд.ММ.ГГ)"] = startDate;
                jsonData.Data["Источник данных"] = dataSource;
                jsonData.Data["Покупка|продажа"] = "Продажа";
                jsonData.Data["Регион"] = row.rowValues[0].Trim();
                jsonData.Data["Регион продажи"] = row.rowValues[0].Trim();
                jsonData.Data["Название товара"] = "Подсолнечное масло";
                jsonData.Data["Базис поставки"] = "EXW";
                jsonData.Data["Объем (приведенные к т.)"] = volume;
                jsonData.Data["Единица измерения товара (стандартные сокращения)"] = UOM;
                jsonData.Data["Признак с НДС|без НДС"] = "с НДС";
                jsonData.Data["Цена предложения, вал.|т"] = row.rowValues[1].Trim();
                jsonData.Data["Цена, руб|т без НДС"] = row.rowValues[1].Trim();
                records.Add(jsonData.Data);
            }
            result.Add("Подсолнечное масло", records);
            return result;
        }


        //Маслянки
        public static Dictionary<string, List<Dictionary<string, string>>> getTable2(string pageText, string culture)
        {
            var result = new Dictionary<string, List<Dictionary<string, string>>>();
            string[] pageLines = pageText.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            Regex reg = new Regex(@"\d");
            string startDate = string.Empty;
            string partString = string.Empty;
            string region = "";

            for (int i = 0; i < pageLines.Length; i++)
            {
                Match match = reg.Match(pageLines[i]);
                if (match.Success || startDate != string.Empty)
                {
                    if (pageLines[i].Contains("за") || pageLines[i].Contains("неделю"))
                        continue;
                    if (pageLines[i].Length < 20)
                    {
                        partString = pageLines[i];
                        continue;
                    }
                    else
                    {
                        pageLines[i] = partString + " " + pageLines[i];
                        partString = string.Empty;
                    }
                    reg = new Regex(@"\d{1,2}[.]\d{1,2}[.]2\d");
                    match = reg.Match(pageLines[i]);
                    if (match.Success)
                    {
                        startDate = match.Value;
                        DateTime date = DateTime.ParseExact(startDate, "d.M.yy", CultureInfo.InvariantCulture);
                        int dayOfWeek = (int)date.DayOfWeek;
                        if (dayOfWeek == 0) dayOfWeek = 7;
                        int daysToFriday = 5 - dayOfWeek;
                        DateTime fridayDate = date.AddDays(daysToFriday);
                        startDate = fridayDate.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
                        Table.tableHeaders = new List<string>() { "Регион", "Цена" };
                    }
                    else
                    {
                        string tempStr = pageLines[i].Trim();
                        if (Regex.Match(tempStr, @"\d").Success)
                        {
                            tempStr = tempStr.Substring(0, tempStr.LastIndexOf(' '));
                            tempStr = tempStr.Substring(0, tempStr.LastIndexOf(' '));
                            string sum3 = tempStr.Substring(tempStr.LastIndexOf(" "));
                            tempStr = tempStr.Substring(0, tempStr.LastIndexOf(' '));
                            string sum2 = tempStr.Substring(tempStr.LastIndexOf(" "));
                            tempStr = tempStr.Substring(0, tempStr.LastIndexOf(' '));
                            string sum1 = tempStr.Substring(tempStr.LastIndexOf(" "));
                            tempStr = tempStr.Substring(0, tempStr.LastIndexOf(' '));
                            region = region + " " + tempStr;
                            Table.addRow(new List<string> { region, sum1 });
                            //Table.addRow(new List<string> { region, sum2 });
                            //Table.addRow(new List<string> { region, sum3 });
                            region = "";
                        }
                        else
                        {
                            region = tempStr;
                        }
                    }
                }
            }
            var records = new List<Dictionary<string, string>>();
            foreach (var row in Table.tableRows)
            {
                JsonRow jsonData = new JsonRow();

                jsonData.Data["Дата (дд.ММ.ГГ)"] = startDate;
                jsonData.Data["Источник данных"] = dataSource;
                jsonData.Data["Покупка|продажа"] = "Покупка";
                jsonData.Data["Регион"] = row.rowValues[0].Trim();
                jsonData.Data["Регион продажи"] = row.rowValues[0].Trim();
                jsonData.Data["Название товара"] = culture;
                if (culture == "Соя")
                    jsonData.Data["Качество"] = "протеин 39%";
                jsonData.Data["Базис поставки"] = "СРТ";
                jsonData.Data["Объем (приведенные к т.)"] = volume;
                jsonData.Data["Единица измерения товара (стандартные сокращения)"] = UOM;
                jsonData.Data["Признак с НДС|без НДС"] = "с НДС";
                jsonData.Data["Цена предложения, вал.|т"] = row.rowValues[1].Trim();
                jsonData.Data["Цена, руб|т без НДС"] = row.rowValues[1].Trim();
                records.Add(jsonData.Data);
            }
            result.Add(culture, records);
            return result;
        }
        public static Dictionary<string, List<Dictionary<string, string>>> getTable(string pageText)
        {
            var result = new Dictionary<string, List<Dictionary<string, string>>>();
            string firstTableText = pageText.Substring(pageText.IndexOf("Товар")).Trim();
            firstTableText = firstTableText.Substring(0, firstTableText.LastIndexOf("Товар"));

            string[] pageLines = firstTableText.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);


            string currentDateStart = pageText.Substring(0, pageText.IndexOf("\n"));
            Regex reg = new Regex(@"\d{2}.\d{2}.\d{4}");
            Match match = reg.Match(currentDateStart);
            if (match.Success)
            {
                currentDateStart = match.Value;
                DateTime date = DateTime.ParseExact(currentDateStart, "dd.MM.yyyy", CultureInfo.InvariantCulture);
                int dayOfWeek = (int)date.DayOfWeek;
                if (dayOfWeek == 0) dayOfWeek = 7;
                if (dayOfWeek == 5)
                {
                    currentDateStart =  date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
                }
                else
                {
                    int daysToPreviousFriday = dayOfWeek - 5;
                    if (daysToPreviousFriday < 0) // Если день раньше пятницы (понедельник-четверг)
                    {
                        daysToPreviousFriday += 7; // Идём к пятнице прошлой недели
                    }
                    DateTime previousFriday = date.AddDays(-daysToPreviousFriday);
                    currentDateStart = previousFriday.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
                }
            }

            string currentCulture = "";
            List<string> cultures = new List<string>() { "Пшеница", "Ячмень", "Кукуруза" };
            bool newCulture = true;
            int currentCultureNumber = 0;
            for (int i = 0; i < pageLines.Length; i++)
            {
                if (i == 0)
                {
                    Table.tableHeaders = pageLines[i].Split(' ').ToList();
                    Table.tableHeaders.RemoveRange(7, Table.tableHeaders.Count - 7);
                }
                else
                {
                    reg = new Regex(@"^[А-я]*$");
                    match = reg.Match(pageLines[i]);
                    if (match.Success)
                        break;

                    if (pageLines[i].StartsWith("Россия"))
                    {
                        if (newCulture)
                        {
                            currentCulture = cultures[currentCultureNumber];
                            currentCultureNumber++;
                            newCulture = false;
                        }
                    }
                    else
                    {
                        newCulture = true;
                    }

                    string tempLine = RemoveDigitsLessThan100FromEnd(pageLines[i]);//Regex.Replace(pageLines[i], @"(\s-{0,1}\d{1,2}){3}", "");
                    string[] lineParts = tempLine.Split(' ');
                    string sum3 = lineParts[lineParts.Length - 1];
                    string sum2 = lineParts[lineParts.Length - 2];
                    string sum1 = lineParts[lineParts.Length - 3];
                    if (!char.IsNumber(sum1[0]))
                        continue;
                    string region = lineParts[0];

                    tempLine = tempLine.Replace(sum3, "");
                    tempLine = tempLine.Replace(sum2, "");
                    tempLine = tempLine.Replace(sum1, "");
                    tempLine = tempLine.Replace(region, "").Trim();

                    string partOfTempLine = tempLine.Substring(3);
                    reg = new Regex(@"[А-ЯA-Z]{3}.*");
                    match = reg.Match(partOfTempLine);
                    string basis = match.Value;
                    string quality = tempLine.Replace(basis, "").Trim();
                    Table.addRow(new List<string>() { currentCulture, region, quality, basis, sum1, sum2, sum3 });
                }
            }
            
            foreach (var culture in cultures)
            {
                var records = new List<Dictionary<string, string>>();
                foreach (var row in Table.tableRows)
                {
                    if (row.rowValues[0].Trim() == culture)
                    {
                        JsonRow jsonData = new JsonRow();
                        jsonData.Data["Название товара"] = row.rowValues[0].Trim();
                        jsonData.Data["Регион"] = row.rowValues[1].Trim();
                        jsonData.Data["Качество"] = row.rowValues[2].Trim().Replace("HRW,", "пр. ").Replace("SRW,", "пр. ");
                        if (row.rowValues[2].Trim().Contains("HRW"))
                            jsonData.Data["Описание товара"] = "HRW";
                        jsonData.Data["Класс"] = GetProteinClass(row.rowValues[2]);

                        string basis = "";
                        string region = "";
                        reg = new Regex(@"^[А-ЯA-Z]{3}");
                        match = reg.Match(row.rowValues[3]);
                        basis = match.Value;
                        region = row.rowValues[3].Replace(basis, "").Trim();
                        jsonData.Data["Базис поставки"] = basis;
                        jsonData.Data["Регион продажи"] = region;
                        jsonData.Data["Источник данных"] = dataSource;
                        jsonData.Data["Объем (приведенные к т.)"] = volume;
                        jsonData.Data["Единица измерения товара (стандартные сокращения)"] = UOM;
                        jsonData.Data["Валюта цены"] = "$";
                        jsonData.Data["Покупка|продажа"] = "СПРОС";
                        jsonData.Data["Признак с НДС|без НДС"] = NDS;
                        jsonData.Data["Дата (дд.ММ.ГГ)"] = currentDateStart;

                        for (int i = 4; i < 7; i++)
                        {
                            jsonData.Data["Дата окончания (дд.ММ.ГГ)"] = convertDate(Table.tableHeaders[i]);
                            jsonData.Data["Цена предложения, вал.|т"] = row.rowValues[i];
                            Dictionary<string, string> newData = new Dictionary<string, string>(jsonData.Data);
                            records.Add(newData);
                        }
                    }
                }
                if (records.Count > 0)
                    result.Add(culture, records);
            }


            Table.tableRows.Clear();
            Table.tableHeaders.Clear();

            string secondTableText = pageText.Substring(pageText.LastIndexOf("Товар")).Trim();

            pageLines = secondTableText.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < pageLines.Length; i++)
            {
                if (i == 0)
                {
                    Table.tableHeaders = pageLines[i].Split(' ').ToList();
                    if (Table.tableHeaders.Count == 6)
                        Table.tableHeaders.RemoveRange(3, 2);
                    else
                        Table.tableHeaders.RemoveRange(3, 1);
                }
                else
                {
                    reg = new Regex(@"^[А-я.,\s]*$");
                    match = reg.Match(pageLines[i]);
                    if (match.Success)
                        continue;
                    string tempStr = string.Empty;
                    if (char.IsNumber(pageLines[i][pageLines[i].Length-1]))
                    {
                        string[] partsOfLine = pageLines[i].Split(' ');
                        for(int j = 0; j < partsOfLine.Length - 3; j++)
                            tempStr = tempStr + " " + partsOfLine[j].Trim();
                    }
                    else
                        tempStr = pageLines[i];
                    tempStr = tempStr.Trim();
                    reg = new Regex(@"(нет\sсделок|\s\d{2}\s\d{3})");
                    MatchCollection ma = reg.Matches(tempStr);
                    string sum = string.Empty;
                    if (ma.Count == 2)
                    {
                        sum = ma[1].Value;
                        if (sum == "нет сделок")
                            continue;
                        tempStr = tempStr.Replace(ma[0].Value, "");
                        tempStr = tempStr.Replace(ma[1].Value, "");
                    }
                    tempStr = tempStr.Replace("RUB", "").Trim();
                    currentCulture = tempStr.Substring(0, tempStr.IndexOf(" "));
                    tempStr = tempStr.Substring(tempStr.IndexOf(" ") + 1);

                    reg = new Regex(@"[А-ЯA-Z]{3}.*");
                    match = reg.Match(tempStr);
                    string basis = match.Value;
                    string quality = tempStr.Replace(basis, "").Trim();


                    Table.addRow(new List<string>() { currentCulture, quality, basis, sum });
                }
            }
            foreach (var culture in cultures)
            {
                var records = new List<Dictionary<string, string>>();
                foreach (var row in Table.tableRows)
                {
                    if (row.rowValues[0].Trim() == culture)
                    {
                        JsonRow jsonData = new JsonRow();
                        jsonData.Data["Название товара"] = row.rowValues[0].Trim();
                        jsonData.Data["Качество"] = row.rowValues[1].Trim().Replace("HRW,", "пр. ").Replace("SRW,", "пр. ");
                        jsonData.Data["Класс"] = row.rowValues[1].Trim();
                        if (row.rowValues[1].Trim().Contains("HRW"))
                            jsonData.Data["Описание товара"] = "HRW";
                        jsonData.Data["Класс"] = GetProteinClass(row.rowValues[1]);
                        string basis = "";
                        string region = "";
                        reg = new Regex(@"^[А-ЯA-Z]{3}");
                        match = reg.Match(row.rowValues[2]);
                        basis = match.Value;
                        region = row.rowValues[2].Replace(basis, "").Trim();
                        jsonData.Data["Базис поставки"] = basis;
                        jsonData.Data["Регион"] = region;
                        jsonData.Data["Регион продажи"] = region;
                        jsonData.Data["Источник данных"] = dataSource;
                        jsonData.Data["Объем (приведенные к т.)"] = volume;
                        jsonData.Data["Единица измерения товара (стандартные сокращения)"] = UOM;
                        jsonData.Data["Валюта цены"] = "₽";
                        jsonData.Data["Признак с НДС|без НДС"] = NDS;
                        jsonData.Data["Дата (дд.ММ.ГГ)"] = currentDateStart;
                        jsonData.Data["Дата окончания (дд.ММ.ГГ)"] = Table.tableHeaders[3].Substring(0, Table.tableHeaders[3].Length - 2) + "20" + Table.tableHeaders[3].Substring(Table.tableHeaders[3].Length - 2);
                        //jsonData.Data["Цена, руб|т без НДС"] = row.rowValues[3];
                        jsonData.Data["Цена предложения, вал.|т"] = row.rowValues[3];
                        jsonData.Data["Покупка|продажа"] = "СПРОС";
                        records.Add(jsonData.Data);
                    }
                }
                if (records.Count > 0)
                {
                    foreach (var record in records)
                    {
                        result[culture].Add(record);
                    }
                }
            }
            return result;
        }
        static string getDate(int month)
        {
            var tempDate = new DateTime(DateTime.Now.Year, month, 1);
            if (tempDate > DateTime.Now)
                tempDate = new DateTime(DateTime.Now.Year - 1, tempDate.Month, tempDate.Day);
            tempDate = tempDate.AddMonths(1).AddDays(-1);
            return tempDate.ToString("dd.MM.yyyy");
        }

        // Границы диапазонов
        private const double FifthClassMax = 11;
        private const double FourthClassMin = 11.5;
        private const double FourthClassMax = 13.0;
        private const double ThirdClassMin = 13.5;
        private const double ThirdClassMax = 14.5;

        public static string GetProteinClass(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;
            if (!input.Contains("пр.") && !input.Contains("SRW") && !input.Contains("HRW"))
                return string.Empty;

            // Очистка строки: оставляем только цифры, запятую, дефис, >, <, %
            input = input.Replace("/", "-");
            string cleaned = Regex.Replace(input, @"[^0-9,\-<>%]", "");
            cleaned = cleaned.TrimStart(new char[] { ',' });

            // Проверяем формат строки
            // Возможные варианты: "12,5%", "13,5-14,0%", ">14,5%", "<10,5%"
            var singleNumberMatch = Regex.Match(cleaned, @"^(\d+,\d|\d+)%$");
            var rangeMatch = Regex.Match(cleaned, @"^(\d+,\d|\d+)-(\d+,\d|\d+)%$");
            var greaterThanMatch = Regex.Match(cleaned, @"^>(\d+,\d)%$");
            var lessThanMatch = Regex.Match(cleaned, @"^<(\d+,\d)%$");

            try
            {
                if (singleNumberMatch.Success)
                {
                    // Обработка одного числа (например, "12,5%")
                    double value = double.Parse(singleNumberMatch.Groups[1].Value.Replace(",", "."), System.Globalization.CultureInfo.InvariantCulture);
                    return ClassifySingleValue(value);
                }
                else if (rangeMatch.Success)
                {
                    // Обработка диапазона (например, "13,5-14,0%")
                    double minValue = double.Parse(rangeMatch.Groups[1].Value.Replace(",", "."), System.Globalization.CultureInfo.InvariantCulture);
                    double maxValue = double.Parse(rangeMatch.Groups[2].Value.Replace(",", "."), System.Globalization.CultureInfo.InvariantCulture);
                    return ClassifyRange(minValue, maxValue);
                }
                else if (greaterThanMatch.Success)
                {
                    // Обработка условия ">" (например, ">14,5%")
                    double value = double.Parse(greaterThanMatch.Groups[1].Value.Replace(",", "."), System.Globalization.CultureInfo.InvariantCulture);
                    if (value <= FourthClassMax && value >= FourthClassMin)
                        return "4 кл.";
                    if (value <= ThirdClassMax && value >= ThirdClassMin)
                        return "3 кл.";
                    return string.Empty;
                }
                else if (lessThanMatch.Success)
                {
                    // Обработка условия "<" (например, "<10,5%")
                    double value = double.Parse(lessThanMatch.Groups[1].Value.Replace(",", "."), System.Globalization.CultureInfo.InvariantCulture);
                    if (value <= FifthClassMax)
                        return "Корм.";
                    if (value >= FourthClassMin && value <= FourthClassMax)
                        return "4 кл.";
                    if (value >= ThirdClassMin && value <= ThirdClassMax)
                        return "3 кл.";
                    return string.Empty;
                }
            }
            catch (FormatException)
            {
                // Если парсинг чисел не удался
                return string.Empty;
            }

            // Если строка не соответствует ни одному формату
            return string.Empty;
        }

        private static string ClassifySingleValue(double value)
        {
            if (value <= FifthClassMax)
                return "Корм.";
            if (value >= FourthClassMin && value <= FourthClassMax)
                return "4 кл.";
            if (value >= ThirdClassMin && value <= ThirdClassMax)
                return "3 кл.";
            return string.Empty;
        }

        private static string ClassifyRange(double minValue, double maxValue)
        {
            // Проверяем, пересекается ли диапазон с [11,5; 12,5]
            if ((minValue <= FourthClassMax && maxValue >= FourthClassMin) ||
                (minValue >= FourthClassMin && minValue <= FourthClassMax) ||
                (maxValue >= FourthClassMin && maxValue <= FourthClassMax))
                return "4 кл.";

            // Проверяем, пересекается ли диапазон с [13,5; 14,5]
            if ((minValue <= ThirdClassMax && maxValue >= ThirdClassMin) ||
                (minValue >= ThirdClassMin && minValue <= ThirdClassMax) ||
                (maxValue >= ThirdClassMin && maxValue <= ThirdClassMax))
                return "3 кл.";

            if (minValue <= FifthClassMax && maxValue <= FifthClassMax)
                return "Корм.";

            return string.Empty;
        }
        public static string RemoveDigitsLessThan100FromEnd(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            // Разбиваем строку на слова
            string[] words = input.Split(new[] { ' ' }, StringSplitOptions.None);

            // Идем с конца и проверяем каждое слово
            for (int i = words.Length - 1; i >= 0; i--)
            {
                // Проверяем, является ли слово числом (включая отрицательные)
                if (Regex.IsMatch(words[i], @"^-?\d+$"))
                {
                    if (int.TryParse(words[i], out int number))
                    {
                        // Если число меньше 100 по модулю, удаляем его
                        if (Math.Abs(number) < 100)
                        {
                            words[i] = "";
                        }
                        else
                        {
                            // Как только находим число >= 100, прерываем цикл
                            break;
                        }
                    }
                }
                else
                {
                    // Если встречаем не число, прерываем цикл
                    break;
                }
            }

            // Собираем строку обратно, убирая лишние пробелы
            return string.Join(" ", words).Trim();
        }
        static string convertDate(string date)
        {
            string currentDate = "";
            switch (date.Substring(0,3))
            {
                case "Янв":
                    currentDate = getDate(1);
                    break;
                case "Фев":
                    currentDate = getDate(2);
                    break;
                case "Мар":
                    currentDate = getDate(3);
                    break;
                case "Апр":
                    currentDate = getDate(4);
                    break;
                case "Май":
                    currentDate = getDate(5);
                    break;
                case "Июн":
                    currentDate = getDate(6);
                    break;
                case "Июл":
                    currentDate = getDate(7);
                    break;
                case "Авг":
                    currentDate = getDate(8);
                    break;
                case "Сен":
                    currentDate = getDate(9);
                    break;
                case "Окт":
                    currentDate = getDate(10);
                    break;
                case "Ноя":
                    currentDate = getDate(11);
                    break;
                case "Дек":
                    currentDate = getDate(12);
                    break;
                default:
                        break;
            }
            return currentDate;
        }
    }
}
