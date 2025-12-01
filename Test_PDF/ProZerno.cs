using iTextSharp.text.pdf.parser;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Test_PDF
{
    internal class ProZerno
    {
        static string dataSource = "ПроЗерно";
        static string volume = "1";
        static string UOM = "т";
        static string basis = "EXW";
        static string currency = "Руб";
        static string NDS = "с НДС";

        public static Dictionary<string, List<Dictionary<string, string>>> getTable(string pageText, int variant = 0, string tableRegion = "")
        {
            var result = new Dictionary<string, List<Dictionary<string, string>>>();

            string[] pageLines = pageText.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            List<string> lastLine = new List<string>();
            string currentRegion = "";
            string currentDate = "";
            string currentCulture = "";
            string cultures = "Пшеница|Рожь|Ячмень|Кукуруза|рапс|подсолнечник|соевые бобы|подсолнечное масло|подсолнечный шрот|Фуражный ячмень|Пивоваренный ячмень";
            string needCultures = "Пшеница|Рожь|Ячмень|Кукуруза|Рапс|Подсолнечник|Горох|Соя|Подсолнечное масло|Подсолнечный шрот|Фуражный ячмень|Пивоваренный ячмень";
            string patternCultures = $@"({cultures}).*?(?=({cultures}|$))";
            string patternDates = @"(\d{1,2}\s[А-я]{2,8}\s2\d)";
            string patternNumbers = @"\d[\d\s]*";
            string patternGeneralRegions = @"Кемеровская|Красноярский";
            List<string> currentCultures = new List<string>();
            List<string> currentDates = new List<string>();
            List<string> commonRegions = new List<string>() { "Центральный район", "Центральное Черноземье", "Юг и Северный Кавказ", "Поволжье", "Южный Урал и Зауралье", "Западная Сибирь", "Восточная Сибирь" };
            string generalString = string.Empty;
            string inputFormat = "d MMM yy";
            string outputFormat = "dd.MM.yyyy";
            if (variant == 3)
            {
                for (int i = 0; i < pageLines.Length; i++)
                {
                    if (i == 0)
                    {
                        MatchCollection matches = Regex.Matches(pageLines[i], @"(\d{2}.\d{2}.\d{2})");

                        foreach (Match match in matches)
                        {
                            currentDates.Add(match.Value);
                        }
                    }
                    else
                    {
                        if (Regex.IsMatch(pageLines[i], @"последнее|изменение"))
                            continue;


                        string pattern = @"\s{1}\d{1,2}\s{1}\d{3}";
                        var matches = Regex.Matches(pageLines[i], pattern);
                        int firstNumberIndex = pageLines[i].IndexOf(matches[0].Value);
                        string crop = pageLines[i].Substring(0, firstNumberIndex).Trim(); 
                        if (crop.Contains("ячмень"))
                        {
                            List<int> numbers = new List<int>();
                            int count = matches.Count > currentDates.Count ? currentDates.Count : matches.Count;
                            for (int j = 0; j < count; j++)
                            {
                                // Удаляем пробелы и парсим в int
                                if (int.TryParse(matches[j].Value.Replace(" ", ""), out int number))
                                {
                                    Table.addRow(new List<string>() { tableRegion, crop, number.ToString(), currentDates[j] });
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                if (variant == 2)
                {
                    for (int i = 1; i < pageLines.Length; i++)
                    {
                        if (i == 1)
                        {
                            MatchCollection matches = Regex.Matches(pageLines[i], @"(\d{2}.\d{2}.\d{2})");

                            foreach (Match match in matches)
                            {
                                currentDates.Add(match.Value);
                            }
                        }
                        else
                        {
                            if (pageLines[i].Contains("СРТ порт"))
                                continue;
                            if (Regex.IsMatch(pageLines[i], @"^\d"))
                                break;
                            if (pageLines[i].Trim() == "")
                                break;
                            if (Regex.IsMatch(pageLines[i], @"последнее|изменение"))
                                continue;


                            string tempStr = pageLines[i].Trim().Replace("  ", "|");
                            tempStr = tempStr.Replace("НДС ", "НДС|");
                            string[] lineParts = tempStr.Split('|');
                            for (int j = 0; j < lineParts.Length - 1; j++)
                            {
                                if (j == 0)
                                    currentRegion = lineParts[j];
                                else
                                {
                                    Table.addRow(new List<string>() { currentRegion, "Горох", lineParts[j], currentDates[j - 1] });
                                }
                            }
                        }
                    }
                }
                else
                {
                    for (int i = 0; i < pageLines.Length; i++)
                    {
                        if (i == 0 && variant == 0)
                            i = 2;
                        if (i == 2 && variant == 0 || i == 0 && variant == 1)
                        {
                            MatchCollection matches = Regex.Matches(pageLines[i], patternCultures);

                            foreach (Match match in matches)
                            {
                                currentCultures.Add(match.Value.Trim());
                            }
                        }
                        else if (i == 3 && variant == 0 || i == 1 && variant == 1)
                        {
                            MatchCollection matches = Regex.Matches(pageLines[i], patternDates);

                            foreach (Match match in matches)
                            {
                                currentDates.Add(match.Value.Trim());
                            }
                        }
                        else
                        {
                            if (variant == 0)
                            {
                                bool check = false;
                                foreach (var cRegion in commonRegions)
                                {
                                    if (pageLines[i].Contains(cRegion))
                                        check = true;
                                }
                                if (check)
                                    continue;
                            }
                            if (Regex.IsMatch(pageLines[i], patternGeneralRegions))
                            {
                                generalString = generalString + pageLines[i];
                                continue;
                            }
                            if (generalString != string.Empty)
                            {
                                pageLines[i] = generalString + " " + pageLines[i];
                                generalString = string.Empty;
                            }
                            if (Regex.IsMatch(pageLines[i], @"^\d"))
                                break;
                            if (string.IsNullOrWhiteSpace(pageLines[i]))
                                break;

                            if (variant == 0)
                            {
                                string tempStr = pageLines[i].Replace("\u00A0", " ");
                                tempStr = tempStr.Replace("  ", " ");
                                var rx = new Regex(@"(?<a>\d{3,7})\s*-\s*(?<b>\d{3,7})", RegexOptions.Compiled);
                                var m = rx.Matches(tempStr);
                                currentRegion = tempStr.Substring(0, m[0].Index).Trim();
                                tempStr = tempStr.Substring(m[0].Index);
                                rx = new Regex(@"(?:(?<a>\d{3,7})\s*-\s*(?<b>\d{3,7})|-|\s\s)", RegexOptions.Compiled);
                                m = rx.Matches(tempStr);

                                var groups = new List<string>(3);
                                for (int f = 0; f < 6; f += 2)
                                {
                                    if (m[f].Length < 3)
                                    {
                                        groups.Add($"Z Z");
                                        continue;
                                    }
                                    string r1 = $"{m[f].Groups["a"].Value}-{m[f].Groups["b"].Value}";
                                    string r2 = $"{m[f + 1].Groups["a"].Value}-{m[f + 1].Groups["b"].Value}";
                                    groups.Add($"{r1} {r2}");
                                }
                                for (int j = 0; j < groups.Count; j++)
                                {
                                    currentCulture = currentCultures[j];
                                    int x = 0;
                                    foreach (var cell in groups[j].Split(' '))
                                    {
                                        if (Regex.IsMatch(cell, @"^\d"))
                                            Table.addRow(new List<string>() { currentRegion, currentCulture, cell, currentDates[(j) * 2 + x] });
                                        x++;
                                    }
                                }
                            }
                            else if (variant == 1)
                            {
                                Match matchReg = Regex.Match(pageLines[i], @"^([А-я]*\s)*");
                                currentRegion = matchReg.Value;

                                MatchCollection matches = Regex.Matches(pageLines[i], @"(\d{2}\s\d{3}|\s{2}(?=(\s)))");

                                for (int j = 0; j < matches.Count; j++)
                                {
                                    if (Regex.IsMatch(matches[j].Value, @"^\d"))
                                    {
                                        currentCulture = currentCultures[j / 2];
                                        Table.addRow(new List<string>() { currentRegion, currentCulture, matches[j].Value, currentDates[j] });
                                    }
                                }
                            }
                        }
                    }
                }
            }

            string[] commonCultureNames = needCultures.Split('|');

            foreach (string commonCultureName in commonCultureNames)
            {
                if (commonCultureName.ToLower() == "ячмень" && variant == 3)
                    continue;

                var records = new List<Dictionary<string, string>>();
                foreach (var row in Table.tableRows)
                {
                    if (row.rowValues[1].ToLower().Contains(commonCultureName.ToLower()) || (row.rowValues[1].ToLower().Contains("соевые") && commonCultureName == "Соя"))
                    {
                        JsonRow jsonData = new JsonRow();
                        jsonData.Data["Название товара"] = row.rowValues[1].Trim();

                        jsonData.Data["Класс"] = row.rowValues[1].Trim();

                        if (commonCultureName == "Соя")
                            jsonData.Data["Качество"] = "протеин 38%";
                        jsonData.Data["Регион продажи"] = row.rowValues[0].Trim();
                        jsonData.Data["Валюта цены"] = currency;
                        jsonData.Data["Источник данных"] = dataSource;
                        jsonData.Data["Объем (приведенные к т.)"] = volume;
                        jsonData.Data["Единица измерения товара (стандартные сокращения)"] = UOM;
                        jsonData.Data["Базис поставки"] = basis;
                        jsonData.Data["Признак с НДС|без НДС"] = NDS;
                        DateTime parsedDate;
                        if (variant == 2 || variant == 3)
                            parsedDate = DateTime.ParseExact(row.rowValues[3], "dd.MM.yy", new CultureInfo("ru-RU"));
                        else
                            parsedDate = DateTime.ParseExact(row.rowValues[3], inputFormat, new CultureInfo("ru-RU"));
                        jsonData.Data["Дата (дд.ММ.ГГ)"] = parsedDate.ToString(outputFormat);

                        MatchCollection matches = Regex.Matches(row.rowValues[2].Trim(), patternNumbers);
                        double[] numbers = new double[matches.Count];
                        for (int i = 0; i < matches.Count; i++)
                        {
                            numbers[i] = double.Parse(matches[i].Value.Replace(" ", ""), CultureInfo.InvariantCulture);
                        }
                        if (numbers.Length == 1)
                        {
                            double averageResult = numbers[0] * 0.9;
                            jsonData.Data["Цена предложения, вал.|т"] = numbers[0].ToString();
                            jsonData.Data["Цена, руб|т без НДС"] = averageResult.ToString();
                        }
                        else if (numbers.Length == 2)
                        {
                            double average = numbers[0];//(numbers[0] + numbers[1]) / 2;
                            double averageResult = average * 0.9;
                            jsonData.Data["Цена предложения, вал.|т"] = average.ToString();
                            jsonData.Data["Цена, руб|т без НДС"] = averageResult.ToString();
                        }

                        records.Add(jsonData.Data);
                    }
                }
                if (records.Count > 0)
                    result.Add(commonCultureName, records);
            }

            
            return result;
        }
    }
}
