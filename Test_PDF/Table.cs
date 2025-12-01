using iTextSharp.text.pdf.parser;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Test_PDF
{
    internal static class Table
    {
        static public List<string> tableHeaders = new List<string>();
        static public List<TableRow> tableRows = new List<TableRow>();

        public static void addRow(List<string> rowData)
        {
            tableRows.Add(new TableRow(rowData.ToList()));
        }
    }

    internal class TableRow
    {
        public List<string> rowValues = new List<string>();
        public TableRow(List<string> rowData)
        {
            this.rowValues = rowData;
        }
    }
    public class JsonRow
    {
        public Dictionary<string, string> Data = new Dictionary<string, string>();
        public JsonRow()
        {
            var entry = new Dictionary<string, string>
            {
                { "Дата (дд.ММ.ГГ)", "" },
                { "Дата окончания (дд.ММ.ГГ)", "" },
                { "Источник данных", "" },
                { "Ссылка", "" },
                { "Покупка|продажа", "" },
                { "Клиент (Название)", "" },
                { "Регион", "" },
                { "Клиент (Контакты)", "" },
                { "Регион продажи", "" },
                { "Название товара", "" },
                { "Описание товара", "" },
                { "Класс", "" },
                { "Качество", "" },
                { "Базис поставки", "" },
                { "Объем (приведенные к т.)", "" },
                { "Единица измерения товара (стандартные сокращения)", "" },
                { "Цена предложения, вал.|т", "" },
                { "Валюта цены", "" },
                { "Признак с НДС|без НДС", "" },
                { "Цена, руб|т без НДС", "" }
            };
            this.Data = entry;
        }
    }

}
