using System.IO;
using System;
using System.Linq;
using System.Text;

using System.Text.RegularExpressions;

//Assumptions:
// - product_number == part_number
// - mfr_product_number == mfr_part_number
// - item_quantity == "stock" in Turn14-Inventory.csv
// - sale_price == retail_price in Turn14-Pricing.csv (unless overriden because it is not profitable)

// I have excluded items that were not profitable at the minimum profitable price
//   However, I have left them in when calculating the CSV file for the delta price and inventory

namespace myApp
{
  struct brand
  {
    public String id, name, aaia;
  }
  class Program
  {
    static void Main(string[] args)
    {
      //
      string brandsPath = "./Turn14-Brands.csv";
      string[] brandLines = System.IO.File.ReadAllLines(brandsPath);

      int rowLength = brandLines.Length;
      int columnLength = brandLines[0].Split(",").Length;
      string[,] data = new string[rowLength, columnLength];

      brand[] arr = new brand[2];
      int brandCount = 0;
      for (int i = 0; i < rowLength; i++)
      {
        string[] column = brandLines[i].Split(",");
        if (column[1] == "Airaid" || column[1] == "aFe")
        {
          arr[brandCount].id = column[0];
          arr[brandCount].name = column[1];
          arr[brandCount].aaia = column[2];
          brandCount++;
        }
      }


      string itemsPath = "./Turn14-Items.csv";

      //reading items csv file into a dictionary with id as the key and all other values in a string delineated by commas as the value

      var productDictionary = File.ReadLines(@itemsPath)
          .Select(line =>
          {
            return Regex.Split(line, ",(?=(?:[^\"]*\"[^\"]*\")*(?![^\"]*\"))");
          })
          .Where(item =>
          {
            try
            {
              //filtering out items that are not from the specified brands
              if (item[13] == arr[0].name || item[13] == arr[1].name)
              {
                using (StreamWriter w = File.AppendText("log.txt"))
                {
                  w.WriteLine("item with id: {0} included because brand is: {1}", item[0], item[13]);
                }
                return true;
              }
              else
              {
                //If the item is not from one of the specified brands, exclude it form the dictionary
                // Console.WriteLine("item with id {0} excluded because brand is {1}", item[0], item[13]);
                using (StreamWriter w = File.AppendText("log.txt"))
                {
                  w.WriteLine("item with id {0} excluded because brand is {1}", item[0], item[13]);
                }
                return false;
              }
            }
            catch (IndexOutOfRangeException)
            {
              using (StreamWriter w = File.AppendText("log.txt"))
              {
                w.WriteLine("item with id {0} excluded because index of brand is out of range", item[0]);
              }
            }
            return false;
          })
          .ToDictionary(data => data[0], data =>
          {
            //string[] dataRow represents one item in the dictionary
            // the order is kept consistent so that the array can be joined together to create a line in the CSV
            string[] dataRow = new string[8];
            if (data.Length > 13)
            {
              dataRow[0] = data[13];
            }
            dataRow[1] = data[2];
            dataRow[2] = data[3];
            dataRow[3] = data[1];
            return dataRow;
          });

      // reading Turn14-Inventory.csv into an array of strings
      string inventoryPath = "./Turn14-Inventory.csv";
      string[] inventoryLines = System.IO.File.ReadAllLines(inventoryPath);
      foreach (string line in inventoryLines)
      {
        // split the string into an array
        // the first entry will be the product id
        string[] row = line.Split(",");
        string id = row[0];
        // if productDictionary has an entry for that id, add the quantity (stock) to the dictionary
        if (productDictionary.ContainsKey(id))
        {
          productDictionary[id][4] = row[4];
        }
      }
      // reading Turn14-Pricin.csv into an array of strings
      string pricingPath = "./Turn14-Pricing.csv";
      string[] pricingLines = System.IO.File.ReadAllLines(pricingPath);
      foreach (string line in pricingLines)
      {
        string[] row = line.Split(",");
        string id = row[0];
        if (productDictionary.ContainsKey(id))
        {
          //set the item_cost
          productDictionary[id][5] = row[1];
          // set the minimum_price
          productDictionary[id][6] = row[4];

        // using Decimals rather than floats to improve accuracy
          Decimal mapPrice;
          Decimal mpPrice;
          Decimal retailPrice;
          Decimal purchaseCost;
          // convert mapPrice, mpPrice, retailPrice, and purchaseCost from string to Decimal
          Decimal.TryParse(row[1]?.ToString(), out purchaseCost);
          Decimal.TryParse(row[4]?.ToString(), out mapPrice);
          Decimal.TryParse(row[5]?.ToString(), out mpPrice);
          Decimal.TryParse(row[6]?.ToString(), out retailPrice);

          purchaseCost = Decimal.Round(purchaseCost, 2);
          mapPrice = Decimal.Round(mapPrice, 2);
          mpPrice = Decimal.Round(mpPrice, 2);
          retailPrice = Decimal.Round(retailPrice, 2);

          Decimal desiredPrice = (Decimal.Parse(row[1]) + 10) * new decimal(1.05);
          desiredPrice = Decimal.Round(desiredPrice, 2);
          if (mapPrice < desiredPrice && retailPrice < desiredPrice && mpPrice > desiredPrice)
          {
            productDictionary[id][7] = row[5];
          }
          else if (mapPrice < desiredPrice && retailPrice > desiredPrice)
          {
            productDictionary[id][7] = row[6];
          }
          else if (mapPrice > desiredPrice)
          {
            productDictionary[id][7] = row[4];
          }
          else
          {
            productDictionary.Remove(id);
            using (StreamWriter w = File.AppendText("log.txt"))
            {
              w.WriteLine("item with id {0} excluded because minimum price: {1} < desired price: {2}", id, mpPrice, desiredPrice);
            }
          }
        }
      }

      string outputFilePath = "./results.csv";

      StringBuilder output = new StringBuilder();
      output.AppendLine("brand_name, product_number, mfr_product_number, item_name, item_quantity, item_cost, minimum_price, sale_price");
      foreach (string[] values in productDictionary.Values)
      {
        output.AppendLine(string.Join(", ", values));
      };

      File.WriteAllText(outputFilePath, output.ToString());
      Random rnd = new Random();

      //making copies of original data
      string[] inventoryLinesOriginal = new string[inventoryLines.Length];
      string[] pricingLinesOriginal = new string[pricingLines.Length];
      inventoryLines.CopyTo(inventoryLinesOriginal, 0);
      pricingLines.CopyTo(pricingLinesOriginal, 0);

      //altering 10 random pricing entires and 10 random stock entries
      for (int i = 0; i < 10; i++)
      {
        int maxLength = inventoryLines.Length;
        int index = rnd.Next(maxLength);
        while (!productDictionary.ContainsKey(inventoryLines[index].Split(",")[0]))
        {
          index = rnd.Next(maxLength);
        }
        string[] inventoryRow = inventoryLines[index].Split(",");
        //saving value of original stock for log file
        string originalStock = inventoryRow[4];
        inventoryRow[4] = rnd.Next(100).ToString();
        inventoryLines[index] = string.Join(", ", inventoryRow);
        // Console.WriteLine("changed value for stock of product id: {0} from {1} to {2}", inventoryRow[0], originalStock, inventoryRow[4]);
        using (StreamWriter w = File.AppendText("log.txt"))
        {
          w.WriteLine("changed value for stock of product id: {0} from {1} to {2}", inventoryRow[0], originalStock, inventoryRow[4]);
        }
      }

      StringBuilder inventoryOutput = new StringBuilder();
      foreach (string line in inventoryLines)
      {
        inventoryOutput.AppendLine(line);
      }
      //writing altered data back to the csv
      File.WriteAllText("./Turn14-Inventory.csv", inventoryOutput.ToString());

      for (int i = 0; i < 10; i++)
      {
        int maxLength = pricingLines.Length;
        int index = rnd.Next(maxLength);
        while (!productDictionary.ContainsKey(pricingLines[index].Split(",")[0]))
        {
          index = rnd.Next(maxLength);
        }
        string[] pricingRow = pricingLines[index].Split(",");
        //pricingFieldIndex can be 4, 5, or 6 depending on whether we are editing "map_price", "jobber_price", or "retail_price"
        int pricingFieldIndex = rnd.Next(3) + 4;
        string originalPrice = pricingRow[pricingFieldIndex];
        pricingRow[pricingFieldIndex] = rnd.Next(100).ToString();
        //Changing has_map to true if we add a map_price
        if (pricingFieldIndex == 4)
        {
          pricingRow[2] = "True";
        }
        pricingLines[index] = string.Join(", ", pricingRow);
        string[] pricingColumns = new string[] { "map_price", "jobber_price", "retail_price" };
        // Console.WriteLine("changed value for {0} price of product id: {1} from {2} to {3}", pricingColumns[pricingFieldIndex - 4], pricingRow[0], originalPrice, pricingRow[pricingFieldIndex]);
        using (StreamWriter w = File.AppendText("log.txt"))
        {
          w.WriteLine("changed value for {0} of product id: {1} from {2} to {3}", pricingColumns[pricingFieldIndex - 4], pricingRow[0], originalPrice, pricingRow[pricingFieldIndex]);
        }
      }

      StringBuilder pricingOutput = new StringBuilder();
      foreach (string line in pricingLines)
      {
        pricingOutput.AppendLine(line);
      }
      // writing altered data back to csv
      File.WriteAllText("./Turn14-Pricing.csv", pricingOutput.ToString());

      foreach (string line in inventoryLines)
      {
        string[] row = line.Split(",");
        string id = row[0];
        if (productDictionary.ContainsKey(id))
        {
            int ogStock;
            int stock;
            int.TryParse(row[4]?.ToString(), out stock);
            //getting previous stock value from dictionary
            int.TryParse(productDictionary[id][4]?.ToString(), out ogStock);
            //setting quantity to diference between previous stock and current stock
            productDictionary[id][4] = Math.Abs(ogStock - stock).ToString();
        }
      }

      foreach (string line in pricingLines)
      {
        string[] row = line.Split(",");

        Decimal mapPrice;
        Decimal mpPrice;
        Decimal retailPrice;
        Decimal purchaseCost;
        Decimal newPrice;

        Decimal.TryParse(row[1]?.ToString(), out purchaseCost);
        Decimal.TryParse(row[4]?.ToString(), out mapPrice);
        Decimal.TryParse(row[5]?.ToString(), out mpPrice);
        Decimal.TryParse(row[6]?.ToString(), out retailPrice);
        Decimal.TryParse(row[7]?.ToString(), out newPrice);

      //rounding to 2 decimal places
        purchaseCost = Decimal.Round(purchaseCost, 2);
        mapPrice = Decimal.Round(mapPrice, 2);
        mpPrice = Decimal.Round(mpPrice, 2);
        retailPrice = Decimal.Round(retailPrice, 2);
        newPrice = Decimal.Round(newPrice, 2);

        Decimal oGPrice;
        Decimal oGmapPrice;

        string id = row[0];




        if (productDictionary.ContainsKey(id))
        {
          Decimal.TryParse(productDictionary[id][7]?.ToString(), out oGPrice);
          Decimal.TryParse(productDictionary[id][6]?.ToString(), out oGmapPrice);

          oGPrice = Decimal.Round(oGPrice, 2);
          oGmapPrice = Decimal.Round(oGmapPrice, 2);

          productDictionary[id][5] = row[1];
          productDictionary[id][6] = Math.Abs(oGmapPrice - mapPrice).ToString();

          Decimal desiredPrice = (Decimal.Parse(row[1]) + new Decimal(10)) * new decimal(1.05);
          desiredPrice = Decimal.Round(desiredPrice, 2);

          if (mapPrice < desiredPrice && retailPrice < desiredPrice && mpPrice > desiredPrice)
          {
            productDictionary[id][7] = Math.Abs(oGPrice - mpPrice).ToString();
            using (StreamWriter w = File.AppendText("log.txt"))
            {
              w.WriteLine("Using minimum profitable price for item with id: {0}", row[0]);
            }
          }
          else if (mapPrice < desiredPrice && retailPrice > desiredPrice)
          {
            productDictionary[id][7] = Math.Abs(oGPrice - retailPrice).ToString();
            using (StreamWriter w = File.AppendText("log.txt"))
            {
              w.WriteLine("Using retail price for item with id: {0}", row[0]);
            }
          }
          else if (mapPrice > desiredPrice)
          {
            productDictionary[id][7] = Math.Abs(oGPrice - mapPrice).ToString();
            using (StreamWriter w = File.AppendText("log.txt"))
            {
              w.WriteLine("Using minimum map price for item with id: {0}", row[0]);
            }
          }
          else
          {
            productDictionary[id][7] = Math.Abs(oGPrice - newPrice).ToString();
            using (StreamWriter w = File.AppendText("log.txt"))
            {
              w.WriteLine("Item with id: {0} is not profitable, but is left in to show delta values", row[0]);
            }
          }
        }
      }

      string deltaOutputPath = "./deltaResults.csv";

      StringBuilder deltaOutput = new StringBuilder();
      deltaOutput.AppendLine("brand_name, product_number, mfr_product_number, item_name, item_quantity, item_cost, minimum_price, sale_price");
      foreach (string[] values in productDictionary.Values)
      {
        deltaOutput.AppendLine(string.Join(", ", values));
      };
      //writing out csv with delta values for price and quantity
      File.WriteAllText(deltaOutputPath, deltaOutput.ToString());

      Console.WriteLine("End of Program");
    }
  }
}
