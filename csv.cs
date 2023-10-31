using HireQuest.Shared.Extensions;
using HireQuest.Shared.ModelContracts;
using HireQuest.Shared.Models;
using Microsoft.PowerBI.Api.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net.WebSockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using YamlDotNet.Core.Tokens;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace HireQuest.Shared.Functions
{
    public static class Converters
    {

        public static byte[] ConvertObjectListToCsvFile<T>(IEnumerable<T> list, string[] propertyNameAllowed = null,
            Type[] typeToAvoid = null)
        {

            // Create a new memory stream to write the csv data to
            MemoryStream stream = new MemoryStream();

            // Create a StreamWriter using the memory stream
            StreamWriter writer = new StreamWriter(stream);

            var properties = typeof(T).GetProperties();

            if (propertyNameAllowed is not null)
            {
                properties = properties.Where(x => propertyNameAllowed.Any(y => y.ToLower() == x.Name.ToLower()))
                    .ToArray();
            }

            if (typeToAvoid is not null)
            {
                properties = properties.Where(x => !typeToAvoid.Any(y => y.Equals(x.PropertyType))).ToArray();
            }

            var columnsName = properties.Select(x => x.Name).Join(",");
            // Write the headers for each column
            writer.WriteLine(columnsName);

            string FormatPropertyValue(object objValue)
            {
                return objValue switch
                {
                    null => string.Empty,
                    string s => s.Replace(",", "-"),
                    DateTime time => time.ToString("MM/dd/yyyy"),
                    _ => objValue.ToString()
                };
            }

            ;

            // Iterate through each object in the list and write its values to the csv file
            foreach (object obj in list)
            {
                var propertiesValues = properties.Select(p => FormatPropertyValue(p.GetValue(obj))).Join(",");

                writer.WriteLine(propertiesValues); // add a line break after each row
            }

            // Flush all data from the writer to the memory stream
            writer.Flush();

            // Return a byte array of the csv data in the memory stream
            return stream.ToArray();
        }



        public static class CsvGenerator
        {
            private enum PropertyType
            {
                BasicType,
                Enumerable,
                DefinedType,
                None
            }

            public class OverrideColumnHeaders
            {
                public string PropertyName { get; set; }
                public string ColumnHeader { get; set; }
            }

            public static byte[] GenerateCsv<T>(IEnumerable<T> items, string[] propertyNameAllowed = null,
                Type[] typeToAvoid = null, OverrideColumnHeaders[] overrideColumnHeaders = null )
            {
                StringBuilder csvContent = new StringBuilder();

                // Headers
                PropertyInfo[] properties = typeof(T).GetProperties();


                if (propertyNameAllowed is not null)
                {
                    properties = properties.Where(x => propertyNameAllowed.Any(y => y.ToLower() == x.Name.ToLower()))
                        .ToArray();
                }

                if (typeToAvoid is not null)
                {
                    properties = properties.Where(x => !typeToAvoid.Any(y => y.Equals(x.PropertyType))).ToArray();
                }

                foreach (var prop in properties)
                {
                    if (typeof(IEnumerable).IsAssignableFrom(prop.PropertyType) && prop.PropertyType != typeof(string))
                    {

                        int maxRepeats = items.Max(item =>
                            (prop.GetValue(item) as IEnumerable)?.Cast<object>()?.Count() ?? 0);
                        for (int i = 1; i <= maxRepeats; i++)
                        {
                            Type genericType = prop.PropertyType.GetGenericArguments().FirstOrDefault();
                            var subProps = genericType.GetProperties();

                            foreach (var subProp in subProps)
                            {
                                var header = overrideColumnHeaders?.FirstOrDefault(x => x.PropertyName == subProp.Name)?.ColumnHeader ?? subProp.Name;
                                csvContent.Append($"{header},");
                            }
                        }
                    }
                    else
                    {
                        csvContent.Append(prop.Name + ",");
                    }
                }

                csvContent.AppendLine();

                string FormatPropertyValue(object objValue)
                {
                    return objValue switch
                    {
                        null => string.Empty,
                        string s => s.Replace(",", "-"),
                        DateTime time => time.ToString("MM/dd/yyyy"),
                        _ => objValue.ToString()
                    };
                }



                //Data
                foreach (var item in items)
                {
                    foreach (var prop in properties)
                    {
                        var value = prop.GetValue(item);

                        if (typeof(IEnumerable).IsAssignableFrom(prop.PropertyType) &&
                            prop.PropertyType != typeof(string))
                        {
                            var list = (value as IEnumerable).Cast<object>().ToList();
                            for (int i = 0; i < list.Count; i++)
                            {
                                Type genericType = list[i].GetType();

                                var subProps = genericType.GetProperties();

                                foreach (var subProp in subProps)
                                {
                                    var a = list[i].GetType().GetProperties();

                                    PropertyInfo propInfo = list[i].GetType()
                                        .GetProperties()
                                        .FirstOrDefault(p => p.Name == subProp.Name);
                                    var subPropValue = propInfo.GetValue(list[i]);
                                    csvContent.Append($"{subPropValue},");
                                }
                            }
                        }
                        else
                        {
                            csvContent.Append(FormatPropertyValue(value) + ",");
                        }
                    }

                    csvContent.AppendLine();
                }

                return Encoding.UTF8.GetBytes(csvContent.ToString());
            }

            public static void WriteCsvFromBytes(byte[] byteData, string filePath)
            {
                // Convert byte array to string using UTF-8 encoding
                string csvData = Encoding.UTF8.GetString(byteData);

                // Write the string to a file
                System.IO.File.WriteAllText(filePath, csvData);
            }

            private class MyObject
            {
                public PropertyType PropertyType { get; set; }
                public PropertyInfo PropertyInfo { get; set; }
                public object Object { get; set; }
                public int Level { get; set; }
                public int Index { get; set; }
                public List<MyObject> Nested { get; set; }

                public MyObject(PropertyType PropertyType, PropertyInfo PropertyInfo, object Object, int Index, int Level, List<MyObject> Nested = null)
                {
                    this.PropertyType = PropertyType;
                    this.PropertyInfo = PropertyInfo;
                    this.Object = Object;
                    this.Nested = Nested;
                    this.Level = Level;
                    this.Index = Index;
                }
            }

            private static string FormatPropertyValue(object objValue)
            {
                return objValue switch
                {
                    null => string.Empty,
                    string s => s.Replace(",", "-"),
                    DateTime time => time.ToString("MM/dd/yyyy"),
                    _ => objValue.ToString()
                };
            }

            public delegate string ValueFormater(object objValue);

            public static byte[] GenerateCsv2<T>(IEnumerable<T> items, string[] propertyNameAllowed = null,
                    Type[] typeToAvoid = null, OverrideColumnHeaders[] overrideColumnHeaders = null, ValueFormater valueFormater = null)
            {
                valueFormater ??= FormatPropertyValue;

                var objectArray = GenerateObjectInArray(items, 0);
                var result = MyObjectToCSV(objectArray, propertyNameAllowed, typeToAvoid, overrideColumnHeaders, valueFormater );
                return result;
            }

            private static List<MyObject> GenerateObjectInArray<T>(IEnumerable<T> items, int level)
            {
                if(items is null) return null;
                var itemsArray = new List<MyObject>();
                for (int i = 0; i < items.Count(); i++)
                {
                    var itemType = items.ElementAt(i).GetType();
                    var propertyInfo = itemType.GetProperties();
                    foreach (var pi in propertyInfo)
                    {
                        var propType = getEnumType(pi);
                        if (propType == PropertyType.BasicType)
                        {
                            itemsArray.Add(new MyObject(propType, pi, pi.GetValue(items.ElementAt(i)), i, level));
                        }
                        else if (propType == PropertyType.DefinedType)
                        {
                            var obj = pi.GetValue(items.ElementAt(i));
                            var objList = obj is not null ? new List<dynamic> { obj } : null;
                            var myObjList = GenerateObjectInArray(objList, level+1);
                            itemsArray.Add(new MyObject(propType, pi, pi.GetValue(items.ElementAt(i)), i, level, myObjList));
                        }
                        else if (propType == PropertyType.Enumerable)
                        {
                            var obj = pi.GetValue(items.ElementAt(i)) as IEnumerable<dynamic>;
                            var myObjList = GenerateObjectInArray(obj, level+1);
                            itemsArray.Add(new MyObject(propType, pi, pi.GetValue(items.ElementAt(i)), i, level, myObjList));
                        }
                    }
                }
                return itemsArray;
            }

            private static byte[] MyObjectToCSV(List<MyObject> items, string[] propertyNameAllowed = null,
                   Type[] typeToAvoid = null, OverrideColumnHeaders[] overrideColumnHeaders = null, ValueFormater valueFormater = null)
            {
                var dic = new Dictionary<string, string[]>();
                var maxIndex = items.Max(x => x.Index) + 1;
                var itemsPerIndex = items.Count() / maxIndex;
                ToDic(items, maxIndex, itemsPerIndex, refIndex: 0, dic, string.Empty, PropertyType.None, propertyNameAllowed, typeToAvoid, valueFormater);

                StringBuilder csvContent = new StringBuilder();

                dic.Keys.ToList().ForEach(x => csvContent.Append(x + ","));
                csvContent.AppendLine();
                dic.Keys.ToList().ForEach(x =>
                {
                    var propName = x.Split(new char[] { '/' }).Last().Trim().ToLower();
                    csvContent.Append(overrideColumnHeaders?.FirstOrDefault(y => y.PropertyName.ToLower() == propName)?.ColumnHeader ?? propName + ",");
                });

                Enumerable.Range(0, maxIndex).ToList().ForEach(idx =>
                {
                    csvContent.AppendLine();
                    foreach (var valueArray in dic.Values)
                    {
                        csvContent.Append(valueArray[idx] + ",");
                    }
                });

                return Encoding.UTF8.GetBytes(csvContent.ToString());
            }

            private static void ToDic (List<MyObject> items,int maxIndex,int itemsPerIndex,int refIndex,Dictionary<string, string[]> dic, string prefix, PropertyType previousPropertyType, string[] propertyNameAllowed = null,
                 Type[] typeToAvoid = null, ValueFormater valueFormater = null)
            {
                if (items is null)
                    return;
                for (int i = 0; i < items.Count; i++)
                {
                    refIndex = items.Count == maxIndex*itemsPerIndex ? i : refIndex;
                    int idx = refIndex / itemsPerIndex;
                    var item = items[i];

                    //Filter by propertyNameAllowed
                    if (propertyNameAllowed?.Contains(item.PropertyInfo.Name) == true)
                    {
                        continue;
                    }
                    //Filter by  typeToAvoid
                    if (typeToAvoid?.Any(x => x == item.PropertyInfo.PropertyType) == true)
                    {
                        continue;
                    }

                    if (item.Nested is null)
                    {
                        if (item.Object is null) continue;
                        var fullPathName = previousPropertyType == PropertyType.Enumerable ? prefix + $"{item.Index}/" + item.PropertyInfo.Name : prefix + item.PropertyInfo.Name;

                        if (dic.ContainsKey(fullPathName))
                        {
                            var values = dic[fullPathName];
                            values[idx] = valueFormater(item.Object);
                        }
                        else
                        {
                            var initValues = new string[maxIndex];
                            initValues[idx] = valueFormater(item.Object);
                            dic.Add(fullPathName, initValues);
                        }
                    }
                    else
                    {
                        var newPrefix =  $"{prefix}{item.PropertyInfo.Name}/";
                        ToDic(item.Nested, maxIndex, itemsPerIndex, refIndex, dic, newPrefix,item.PropertyType, propertyNameAllowed, typeToAvoid, valueFormater);
                    }
                }
            }

            private static PropertyType getEnumType(PropertyInfo propInfo)
            {
                Type propType = propInfo.PropertyType;

                if (typeof(IEnumerable).IsAssignableFrom(propType) &&
                    propType != typeof(string))
                {
                    return PropertyType.Enumerable;
                }
                else if (!propType.IsPrimitive &&
                    !propType.IsValueType &&
                    propType != typeof(string) &&
                    propType != typeof(decimal) &&
                    !typeof(Array).IsAssignableFrom(propType) &&
                    !typeof(Delegate).IsAssignableFrom(propType))
                {
                    return PropertyType.DefinedType;
                }
                else
                {
                    return PropertyType.BasicType;
                }
            }
        }
    }
}



public static class Example
{
    public static List<Person> Example1 = new List<Person>
        {
            new Person
            {
                Name = "John",
                Age = 30,
                Hobbies = new List<Hobby>
                {
                    new Hobby { HobbyName = "Reading", Details =  "Fiction"  },
                    new Hobby { HobbyName = "Swimming", Details = "Pool" }
                }
            },
            new Person
            {
                Name = "Doe",
                Age = 25,
                Hobbies = new List<Hobby>
                {
                    new Hobby { HobbyName = "Hiking", Details = "Mountains"}
                }
            }
        };

    public static List<Person> Example2 = new List<Person>
        {
            new Person
            {
                Name = "John",
                Age = 30,
                Hobbies = new List<Hobby>
                {
                    new Hobby { HobbyName = "Reading", Details =  "Fiction", Category = new List<Category>{
                        new Category {
                            CatName = "catName1",
                            Other="OtherCat1" } }  },
                    new Hobby { HobbyName = "Swimming", Details = "Pool" }
                },
                Category = new(){ CatName = "catName0",
                            Other="OtherCat0"}
            },
            new Person
            {
                Name = "Doe",
                Age = 25,
                Hobbies = new List<Hobby>
                {
                    new Hobby { HobbyName = "Hiking", Details = "Mountains"}
                }
            },
             new Person
            {
                Name = "Pedro",
                Age = 46,
                Hobbies = new List<Hobby>
                {
                    new Hobby { HobbyName = "Hobb2", Details = "HobDeta2", Category = new List<Category>{
                        new Category {
                            CatName = "catName2",
                            Other="OtherCat2" },
                        new Category {
                            CatName = "catName3",
                            Other="OtherCat3" } }  ,}
                }
            }
        };
}

[HttpGet("csv")]
   public async Task<IActionResult> CSV()
   {
       List<Person> people = Example.Example2;

       var byteArray = Converters.CsvGenerator.GenerateCsv2(people);
       Converters.CsvGenerator.WriteCsvFromBytes(byteArray, "path_to_save.csv");
       return Ok("Tyler I will help you not to die");
   }
