using System;
using System.Collections.Generic;
using System.IO;
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.parser;
using Newtonsoft.Json;
using Test_PDF;
using Path = System.IO.Path;
using MimeKit;
using System.Threading;
using System.Xml.Linq;
using System.Xml;
using System.Linq;

class Program
{
    static string logPath = "";
    static bool ikar = false;
    static string ikarDest = "";
    static string mail_Folder_Path = "";
    static void Main(string[] args)
    {
        logPath = Path.Combine("fix", "errors.txt");

        string folderPath = args[0];
        mail_Folder_Path = args[0];
        if (!Directory.Exists(folderPath))
        {
            File.AppendAllText(logPath, $"Папка {folderPath} не найдена.\n");
            return;
        }

        string[] xmlFiles = Directory.GetFiles(Path.Combine(folderPath, "self"), "*.xml");
        foreach (string xmlFile in xmlFiles)
        {
            ExtractTextFromXml(xmlFile);
        }

        string[] mailFiles = Directory.GetFiles(Path.Combine(folderPath, "mails"), "*.eml");
        foreach (string pdfFile in mailFiles)
        {
            ExtractTextFromEml(pdfFile);
        }

        string tempFolderPath = Path.Combine(Path.GetDirectoryName(args[0]), "Икар");
        if (Directory.Exists(tempFolderPath))
        {
            ikarDest = tempFolderPath;
        }

        string[] pdfFiles = Directory.GetFiles(folderPath, "*.pdf");

        foreach (string pdfFile in pdfFiles)
        {
            ExtractTextFromPdf(pdfFile);
        }

        if (ikarDest != "")
        {
            ikar = true;
            folderPath = Path.Combine(Path.GetDirectoryName(args[0]), "Икар");
            string jsonData = "";
            if (File.Exists(Path.Combine(folderPath, "result.json")))
            {
                jsonData = File.ReadAllText(Path.Combine(folderPath, "result.json"));
            }
            var result = JsonConvert.DeserializeObject<Dictionary<string, List<Dictionary<string, string>>>>(jsonData);
            folderPath = Path.Combine(folderPath, "temp");
            if (!Directory.Exists(folderPath))
            {
                File.AppendAllText(logPath, $"Папка {folderPath} не найдена.\n");
                return;
            }

            pdfFiles = Directory.GetFiles(folderPath, "*.pdf");

            foreach (string pdfFile in pdfFiles)
            {
                ExtractTextFromPdf(pdfFile, result);
            }
        }

        //Console.ReadKey();
    
    }

    static void ExtractTextFromXml(string xmlFilePath)
    {
        try
        {
            string xmlData = File.ReadAllText(xmlFilePath);
            XDocument xmlDoc = XDocument.Parse(xmlData);
            var currencyDoc = new XDocument(new XElement("root"));
            var quotesDoc = new XDocument(new XElement("root"));
            var futuresDoc = new XDocument(new XElement("root"));
            var jsonObject = new Dictionary<string, object>();
            bool delFile = false;

            // Обрабатываем теги <Tags>

            foreach (var element in xmlDoc.Root.Elements())
            {
                switch (element.Name.LocalName)
                {
                    case "Currency":
                        // Добавляем Currency в отдельный документ
                        currencyDoc.Root.Add(element);
                        var currencyData = element.Elements("Data")
                            .Select(data => data.Elements("field")
                                .ToDictionary(field => field.Attribute("name")?.Value, field => field.Value))
                            .ToList();
                        jsonObject["Currency"] = currencyData;
                        break;

                    case "Quotes":
                        // Добавляем Quotes во отдельный документ
                        quotesDoc.Root.Add(element);
                        var quotesData = element.Elements("Data")
                            .Select(data => data.Elements("field")
                                .ToDictionary(field => field.Attribute("name")?.Value, field => field.Value))
                            .ToList();
                        jsonObject["Quotes"] = quotesData;
                        break;
                    case "Futures":
                        // Добавляем Futures во отдельный документ
                        futuresDoc.Root.Add(element);
                        var futuresData = element.Elements("Data")
                            .Select(data => data.Elements("field")
                                .ToDictionary(field => field.Attribute("name")?.Value, field => field.Value))
                            .ToList();
                        jsonObject["Futures"] = futuresData;
                        break;
                    case "Tags":
                        var tags = element.Elements("tag")
                            .ToDictionary(tag => tag.Attribute("name")?.Value, tag => tag.Value);
                        string tagsResult = JsonConvert.SerializeObject(tags, Newtonsoft.Json.Formatting.Indented);
                        string tagsFilePath = Path.ChangeExtension(xmlFilePath, "tags.json");
                        if (!File.Exists(tagsFilePath))
                            File.WriteAllText(tagsFilePath, tagsResult);
                        break;
                    case "Cultures":
                        foreach (var culture in element.Elements())
                        {
                            if (culture.Elements("Data") != null)
                            {
                                var CulturesData = culture.Elements("Data")
                                    .Select(data =>
                                        data.Elements("field")
                                            .ToDictionary(field => field.Attribute("name")?.Value, field => field.Value))
                                    .ToList();
                                jsonObject[culture.Attribute("name").Value] = CulturesData;
                            }
                        }
                        delFile = true;
                        break;
                    default:
                        if (element.Elements("Data") != null)
                        {
                            var defaultData = element.Elements("Data")
                                .Select(data =>
                                    data.Elements("field")
                                        .ToDictionary(field => field.Attribute("name")?.Value, field => field.Value))
                                .ToList();
                            jsonObject[element.Name.LocalName] = defaultData;
                        }
                        break;
                }
            }

            if (delFile)
            {
                string jsonResult = JsonConvert.SerializeObject(jsonObject);
                string jsonFilePath = Path.ChangeExtension(xmlFilePath, ".json");
                File.WriteAllText(jsonFilePath, jsonResult);
                File.Delete(xmlFilePath);
            }
            else
            {
                if(!xmlFilePath.Contains(jsonObject.Keys.First()))
                {
                    string currencyFilePath = Path.ChangeExtension(xmlFilePath, ".Currency.xml");
                    string quotesFilePath = Path.ChangeExtension(xmlFilePath, ".Quotes.xml");
                    string futuresFilePath = Path.ChangeExtension(xmlFilePath, ".Futures.xml");
                    bool fileSaved = false;
                    if (currencyDoc.Root.HasElements)
                    {
                        currencyDoc.Save(currencyFilePath);
                        fileSaved = true;
                    }

                    if (quotesDoc.Root.HasElements)
                    {
                        quotesDoc.Save(quotesFilePath);
                        fileSaved = true;
                    }

                    if (futuresDoc.Root.HasElements)
                    {
                        futuresDoc.Save(futuresFilePath);
                        fileSaved = true;
                    }

                    if (!fileSaved)
                    {
                        string newFileName = Path.ChangeExtension(xmlFilePath, jsonObject.Keys.First() + ".xml");
                        File.Move(xmlFilePath, newFileName);
                    }
                    else
                    {
                        File.Delete(xmlFilePath);
                    }
                    //File.Copy(newFileName, Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(xmlFilePath))), "result", Path.GetFileName(newFileName)));
                }
            }
        }
        catch (Exception ex)
        {
            File.AppendAllText(logPath, $"Ошибка при чтении файла {Path.GetFileName(xmlFilePath)}: {ex.Message}\n");
        }
    }
    static void ExtractTextFromEml(string emlFilePath)
    {
        var result = new Dictionary<string, List<Dictionary<string, string>>>();
        string dataSource = string.Empty;
        
        try
        {
            MimeMessage message;
            using (var stream = File.OpenRead(emlFilePath))
            {
                message = MimeMessage.Load(stream);
            }
            string bodyText = message.TextBody ?? message.HtmlBody;
            if (!string.IsNullOrEmpty(bodyText))
            {
                Start:
                int startIndex = bodyText.IndexOf("Закупочные цены");
                if (startIndex != -1)
                {
                    int endIndex = bodyText.Substring(startIndex + 40).IndexOf("Закупочные цены");
                    if (endIndex == -1)
                    {
                        endIndex = bodyText.Substring(startIndex + 40).IndexOf("ООО «Тамбовский бекон»");
                    }
                    if (endIndex != -1)
                    {
                        string tempText = bodyText.Substring(startIndex, endIndex + 40);
                        var resultTable = Tambov.getTable(tempText);
                        bodyText = bodyText.Substring(startIndex + endIndex);

                        foreach (var tableValue in resultTable)
                        {
                            if (result.ContainsKey(tableValue.Key))
                            {
                                foreach (var values in resultTable[tableValue.Key])
                                {
                                    result[tableValue.Key].Add(values);
                                }
                            }
                            else
                            {
                                result.Add(tableValue.Key, tableValue.Value);
                            }
                        }
                        dataSource = "ТамбовскийБекон";
                        Table.tableRows.Clear();
                        goto Start;
                    }
                }


                if (result.Count > 0)
                {
                    string outputJsonContent = JsonConvert.SerializeObject(result, Newtonsoft.Json.Formatting.Indented);
                    File.WriteAllText(Path.Combine(Path.GetDirectoryName(mail_Folder_Path), dataSource, "result.json"), outputJsonContent);
                    //File.WriteAllText(Path.Combine(Path.GetDirectoryName(logPath), dataSource, "result.json"), outputJsonContent);
                }
            }
        
        }
        catch (Exception ex)
        {
            File.AppendAllText(logPath, $"Ошибка при чтении файла {Path.GetFileName(emlFilePath)}: {ex.Message}\n");
        }
    }

    static void ExtractTextFromPdf(string pdfFilePath, Dictionary<string, List<Dictionary<string, string>>> result = null)
    {
        if (result == null)
            result = new Dictionary<string, List<Dictionary<string, string>>>();

        string dataSource = string.Empty;
        /*
        try
        {*/
            using (PdfReader reader = new PdfReader(pdfFilePath))
            {
                for (int i = 1; i <= reader.NumberOfPages; i++)
                {
                    ITextExtractionStrategy strategy = new SimpleTextExtractionStrategy();
                    string pageText = PdfTextExtractor.GetTextFromPage(reader, i, strategy);

                    /*
                     * PLATTS
                     */

                    int startIndex = pageText.IndexOf("Platts wheat assessments");
                    int endIndex = pageText.IndexOf("CFR Indonesia wheat price matrix");

                    if (startIndex != -1 && endIndex != -1 && startIndex < endIndex)
                    {
                        string tempPageText = pageText.Substring(startIndex + "Platts wheat assessments".Length, endIndex - "Platts wheat assessments".Length - startIndex);
                        result.Add("Пшеница", Platts.getTable(tempPageText, "Wheat"));
                        dataSource = "Platts";
                        Table.tableRows.Clear();
                    }

                    startIndex = pageText.IndexOf("Platts corn assessments");
                    endIndex = pageText.IndexOf("Corn arbitrage price matrix");

                    if (startIndex != -1 && endIndex != -1 && startIndex < endIndex)
                    {
                        pageText = pageText.Substring(startIndex + "Platts corn assessments".Length, endIndex - "Platts corn assessments".Length - startIndex);
                        result.Add("Кукуруза", Platts.getTable(pageText, "Corn"));
                        dataSource = "Platts";
                        Table.tableRows.Clear();
                    }

                    startIndex = pageText.IndexOf("Platts oilseeds assessments");
                    endIndex = pageText.IndexOf("Platts soybean crush assessments");

                    if (startIndex != -1 && endIndex != -1 && startIndex < endIndex)
                    {
                        pageText = pageText.Substring(startIndex + "Platts oilseeds assessments".Length, endIndex - "Platts oilseeds assessments".Length - startIndex);
                        result.Add("Соя", Platts.getTable(pageText, "SOYBEX|Soybeans"));
                        dataSource = "Platts";
                        Table.tableRows.Clear();
                    }
                    startIndex = pageText.IndexOf("Sunflower Oil");
                    endIndex = pageText.IndexOf("Heards (continued)");

                    if (startIndex != -1 && endIndex != -1 && startIndex < endIndex)
                    {
                        string pageTextNext = PdfTextExtractor.GetTextFromPage(reader, i + 1, strategy);
                        pageTextNext = pageTextNext.Substring(startIndex + "Sunflower Oil".Length);

                        result.Add("Подсолнечное масло", Platts.getTableOil(pageTextNext));
                        dataSource = "Platts";
                        Table.tableRows.Clear();
                    }


                    /*
                     * ProZerno
                     */

                    startIndex = pageText.IndexOf("Продовольственное зерно: средние цены ");

                    if (startIndex != -1)
                    {
                        pageText = pageText.Substring(startIndex + "Продовольственное зерно: средние цены ".Length);
                        var resultTable = ProZerno.getTable(pageText);
                        foreach (var tableValue in resultTable)
                        {
                            if (result.ContainsKey(tableValue.Key))
                            {
                                foreach (var values in resultTable[tableValue.Key])
                                {
                                    result[tableValue.Key].Add(values);
                                }
                            }
                            else
                            {
                                result.Add(tableValue.Key, tableValue.Value);
                            }
                        }
                        dataSource = "ProZerno";
                        Table.tableRows.Clear();
                    }

                    startIndex = pageText.IndexOf("Фуражное зерно: средние цены ");

                    if (startIndex != -1)
                    {
                        pageText = pageText.Substring(startIndex + "Фуражное зерно: средние цены ".Length);
                        var resultTable = ProZerno.getTable(pageText);
                        foreach (var tableValue in resultTable)
                        {
                            if (result.ContainsKey(tableValue.Key))
                            {
                                foreach (var values in resultTable[tableValue.Key])
                                {
                                    result[tableValue.Key].Add(values);
                                }
                            }
                            else
                            {
                                result.Add(tableValue.Key, tableValue.Value);
                            }
                        }
                        dataSource = "ProZerno";
                        Table.tableRows.Clear();
                    }


                    startIndex = pageText.IndexOf("рапс подсолнечник соевые бобы");

                    if (startIndex != -1)
                    {
                        endIndex = pageText.Substring(startIndex).IndexOf("Средние цены в регионах России,");
                        string tempPageText = pageText.Substring(startIndex);
                        if (endIndex != -1)
                        {
                            pageText = pageText.Substring(startIndex, endIndex);
                            var resultTable = ProZerno.getTable(pageText, 1);
                            foreach (var tableValue in resultTable)
                            {
                                if (result.ContainsKey(tableValue.Key))
                                {
                                    foreach (var values in resultTable[tableValue.Key])
                                    {
                                        result[tableValue.Key].Add(values);
                                    }
                                }
                                else
                                {
                                    result.Add(tableValue.Key, tableValue.Value);
                                }
                            }
                            dataSource = "ProZerno";
                            Table.tableRows.Clear();

                            tempPageText = tempPageText.Substring(endIndex);
                            tempPageText = tempPageText.Substring(0, tempPageText.IndexOf("Индекс цен"));
                            pageText = tempPageText.Substring(tempPageText.IndexOf("подсолнечник"));
                            resultTable = ProZerno.getTable(pageText, 1);
                            if (resultTable.Keys.Contains("Подсолнечник"))
                                resultTable.Remove("Подсолнечник");
                            foreach (var tableValue in resultTable)
                            {
                                if (result.ContainsKey(tableValue.Key))
                                {
                                    foreach (var values in resultTable[tableValue.Key])
                                    {
                                        result[tableValue.Key].Add(values);
                                    }
                                }
                                else
                                {
                                    result.Add(tableValue.Key, tableValue.Value);
                                }
                            }
                            dataSource = "ProZerno";
                            Table.tableRows.Clear();
                        }
                    }

                    startIndex = pageText.IndexOf("Приложения: динамики цен на зерн");
                    if (startIndex != -1)
                    {
                        string mainString = "Средние цены на зерно";
                        List<string> regions = new List<string>() { "Европейская Россия" };
                        string tempPageText = pageText.Substring(startIndex);
                        while(tempPageText.Contains(mainString))
                        {
                            tempPageText = tempPageText.Substring(tempPageText.IndexOf(mainString) + 1);
                            string line = tempPageText.Substring(0, tempPageText.IndexOf("\n"));
                            string OurRegion = "";
                            foreach(var region in regions)
                            {
                                if (line.Contains(region))
                                {
                                    OurRegion = region;
                                    break;
                                }
                            }
                            if (OurRegion != "")
                            {
                                tempPageText = tempPageText.Substring(tempPageText.IndexOf("Товар"));
                                if (tempPageText.Contains(mainString))
                                    pageText = tempPageText.Substring(0, tempPageText.IndexOf(mainString));
                                var resultTable = ProZerno.getTable(pageText, 3, OurRegion);
                                foreach (var tableValue in resultTable)
                                {
                                    result.Add(tableValue.Key, tableValue.Value);
                                }
                                dataSource = "ProZerno";
                                Table.tableRows.Clear();
                            }
                        }
                    }

                    startIndex = pageText.IndexOf("Горох: средние цены,");
                    endIndex = pageText.IndexOf("Цены ячмень фуражной");
                    if (endIndex == -1)
                        endIndex = pageText.IndexOf("Мукомольная продукция");

                    if (startIndex != -1 && endIndex  != -1 && startIndex < endIndex)
                    {
                        pageText = pageText.Substring(startIndex + "Горох: средние цены,".Length, endIndex - "Горох: средние цены,".Length - startIndex);
                        var resultTable = ProZerno.getTable(pageText, 2);
                        foreach (var tableValue in resultTable)
                        {
                            result.Add(tableValue.Key, tableValue.Value);
                        }
                        dataSource = "ProZerno";
                        Table.tableRows.Clear();
                    }

                    startIndex = pageText.IndexOf("Цены производителей подсолнечного масла");

                    if (startIndex != -1)
                    {
                        endIndex = pageText.IndexOf("Цены на подсолнечное");
                        if (endIndex != -1)
                        {
                            if (ikar)
                            {
                                pageText = pageText.Substring(startIndex);
                                string date = pageText.Substring(pageText.IndexOf("Цены на"));
                                date = date.Substring(0, date.IndexOf("\n"));
                                pageText = pageText.Substring(pageText.IndexOf("min max"));

                                var resultTable = Ikar.getTable3(pageText, date);
                                foreach (var tableValue in resultTable)
                                {
                                    if (result.ContainsKey(tableValue.Key))
                                    {
                                        foreach (var values in resultTable[tableValue.Key])
                                        {
                                            result[tableValue.Key].Add(values);
                                        }
                                    }
                                    else
                                    {
                                        result.Add(tableValue.Key, tableValue.Value);
                                    }
                                }
                                dataSource = "Икар";
                                Table.tableRows.Clear();
                            }
                            else if (ikarDest != "")
                            {
                                string pdfPath = Path.Combine(ikarDest, "temp", Path.GetFileName(pdfFilePath));
                                if (!File.Exists(pdfPath))
                                    File.Copy(pdfFilePath, pdfPath);
                            }
                        }
                    }
                    startIndex = pageText.IndexOf("растительные масла (");
                    endIndex = pageText.IndexOf("Источник: ИКАР");

                    if (startIndex != -1 && endIndex != -1 && startIndex < endIndex)
                    {
                        if (ikar)
                        {
                            pageText = pageText.Substring(startIndex, endIndex - startIndex);
                            var resultTable = Ikar.getTable4(pageText);
                            foreach (var tableValue in resultTable)
                            {
                                if (result.ContainsKey(tableValue.Key))
                                {
                                    foreach (var values in resultTable[tableValue.Key])
                                    {
                                        result[tableValue.Key].Add(values);
                                    }
                                }
                                else
                                {
                                    result.Add(tableValue.Key, tableValue.Value);
                                }
                            }

                            dataSource = "Икар";
                            Table.tableRows.Clear();
                        }
                        else if (ikarDest != "")
                        {
                            string pdfPath = Path.Combine(ikarDest, "temp", Path.GetFileName(pdfFilePath));
                            if (!File.Exists(pdfPath))
                                File.Copy(pdfFilePath, pdfPath);
                        }
                    }

                    startIndex = pageText.IndexOf("шрот ($/t");
                    endIndex = pageText.IndexOf("Источник: ИКАР");

                    if (startIndex != -1 && endIndex != -1 && startIndex < endIndex)
                    {
                        if (ikar)
                        {
                            pageText = pageText.Substring(startIndex, endIndex - startIndex);
                            var resultTable = Ikar.getTable4(pageText);
                            foreach (var tableValue in resultTable)
                            {
                                if (result.ContainsKey(tableValue.Key))
                                {
                                    foreach (var values in resultTable[tableValue.Key])
                                    {
                                        result[tableValue.Key].Add(values);
                                    }
                                }
                                else
                                {
                                    result.Add(tableValue.Key, tableValue.Value);
                                }
                            }
                            dataSource = "Икар";
                            Table.tableRows.Clear();
                        }
                        else if (ikarDest != "")
                        {
                            string pdfPath = Path.Combine(ikarDest, "temp", Path.GetFileName(pdfFilePath));
                            if (!File.Exists(pdfPath))
                                File.Copy(pdfFilePath, pdfPath);
                        }
                    }

                    startIndex = pageText.IndexOf("Ценовые индексы FOB на");

                    if (startIndex != -1)
                    {
                        if (ikar)
                        {
                            pageText = pageText.Substring(startIndex);
                            var resultTable = Ikar.getTable(pageText);
                            foreach (var tableValue in resultTable)
                            {
                                if (result.ContainsKey(tableValue.Key))
                                {
                                    foreach (var values in resultTable[tableValue.Key])
                                    {
                                        result[tableValue.Key].Add(values);
                                    }
                                }
                                else
                                {
                                    result.Add(tableValue.Key, tableValue.Value);
                                }
                            }
                            dataSource = "Икар";
                            Table.tableRows.Clear();
                        }
                        else if (ikarDest != "")
                        {
                            string pdfPath = Path.Combine(ikarDest, "temp", Path.GetFileName(pdfFilePath));
                            if (!File.Exists(pdfPath))
                                File.Copy(pdfFilePath, pdfPath);
                        }
                    }


                    startIndex = pageText.IndexOf("Динамика российских и мировых цен на подсолнечник");

                    if (startIndex != -1)
                    {
                        endIndex = pageText.Substring(startIndex).IndexOf("Источник: ИКАР");
                        if (endIndex != -1)
                        {
                            if (ikar)
                            {
                                pageText = pageText.Substring(startIndex, endIndex);
                                var resultTable = Ikar.getTable2(pageText, "Подсолнечник");
                                foreach (var tableValue in resultTable)
                                {
                                    if (result.ContainsKey(tableValue.Key))
                                    {
                                        foreach (var values in resultTable[tableValue.Key])
                                        {
                                            result[tableValue.Key].Add(values);
                                        }
                                    }
                                    else
                                    {
                                        result.Add(tableValue.Key, tableValue.Value);
                                    }
                                }
                                dataSource = "Икар";
                                Table.tableRows.Clear();
                            }
                            else if (ikarDest != "")
                            {
                                string pdfPath = Path.Combine(ikarDest, "temp", Path.GetFileName(pdfFilePath));
                                if (!File.Exists(pdfPath))
                                    File.Copy(pdfFilePath, pdfPath);
                            }
                        }
                    }

                    startIndex = pageText.IndexOf("Динамика российских");

                    if (startIndex != -1)
                    {
                        string tempText = pageText.Substring(startIndex).Trim();
                        tempText = tempText.Substring(0, tempText.Length > 100 ? 100 : tempText.Length - 1);
                        endIndex = pageText.Substring(startIndex).IndexOf("Источник: ИКАР");
                        if (endIndex != -1 && tempText.Contains("мировых цен на сою"))
                        {
                            if (ikar)
                            {
                                pageText = pageText.Substring(startIndex, endIndex);
                                var resultTable = Ikar.getTable2(pageText, "Соя");
                                foreach (var tableValue in resultTable)
                                {
                                    if (result.ContainsKey(tableValue.Key))
                                    {
                                        foreach (var values in resultTable[tableValue.Key])
                                        {
                                            result[tableValue.Key].Add(values);
                                        }
                                    }
                                    else
                                    {
                                        result.Add(tableValue.Key, tableValue.Value);
                                    }
                                }
                                dataSource = "Икар";
                                Table.tableRows.Clear();
                            }
                            else if (ikarDest != "")
                            {
                                string pdfPath = Path.Combine(ikarDest, "temp", Path.GetFileName(pdfFilePath));
                                if (!File.Exists(pdfPath))
                                    File.Copy(pdfFilePath, pdfPath);
                            }
                        }
                    }

                    startIndex = pageText.IndexOf("Динамика российских");

                    if (startIndex != -1)
                    {
                        string tempText = pageText.Substring(startIndex).Trim();
                        tempText = tempText.Substring(0, tempText.Length > 100 ? 100 : tempText.Length - 1);
                        endIndex = pageText.Substring(startIndex).IndexOf("Источник: ИКАР");
                        if (endIndex != -1 && tempText.Contains("рапс"))
                        {
                            if (ikar)
                            {
                                pageText = pageText.Substring(startIndex, endIndex);
                                var resultTable = Ikar.getTable2(pageText, "Рапс");
                                foreach (var tableValue in resultTable)
                                {
                                    if (result.ContainsKey(tableValue.Key))
                                    {
                                        foreach (var values in resultTable[tableValue.Key])
                                        {
                                            result[tableValue.Key].Add(values);
                                        }
                                    }
                                    else
                                    {
                                        result.Add(tableValue.Key, tableValue.Value);
                                    }
                                }
                                dataSource = "Икар";
                                Table.tableRows.Clear();
                            }
                            else if (ikarDest != "")
                            {
                                string pdfPath = Path.Combine(ikarDest, "temp", Path.GetFileName(pdfFilePath));
                                if (!File.Exists(pdfPath))
                                    File.Copy(pdfFilePath, pdfPath);
                            }
                        }
                    }

                }
            }
            if (result.Count > 0)
            {
                List<string> list = result.Keys.ToList();
                foreach(var key in list)
                {
                    if (result[key].Count == 0)
                        result.Remove(key);
                }
                string outputJsonContent = JsonConvert.SerializeObject(result, Newtonsoft.Json.Formatting.Indented);
                //{Path.GetFileNameWithoutExtension(pdfFilePath)}_
                File.WriteAllText(Path.Combine(Path.GetDirectoryName(mail_Folder_Path), dataSource, $"result.json"), outputJsonContent);
            }
        /*
        }
        catch (Exception ex)
        {
            File.AppendAllText(logPath, $"Ошибка при чтении файла {Path.GetFileName(pdfFilePath)}: {ex.Message}\n");
            File.Copy(pdfFilePath, "fix/" + Path.GetFileName(pdfFilePath));
        }*/
    }
}
