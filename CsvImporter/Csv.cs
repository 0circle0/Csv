using Microsoft.VisualBasic.FileIO;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text;

namespace CsvImporter
{
    public class Csv
    {
        #region Export

        /*
         * Known Bugs:
         * The DateTime exported does not exactly match the DateTime from import. 
         * It Exports as YEAR/MONTH/DAY 12 HOUR TIME AM/PM
         * Import as MONTH/DAY/YEAR 24 HOUR TIME
         * 
         * Fix
         * Would have to detect if the value is a DateTime and adjust for it
         */

        /// <summary>
        /// Exports a CSV from a List of Objects using the DisplayAttribute on Properties as Headers
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="objects"></param>
        /// <returns></returns>
        public static async Task<Result> Export<T>(List<T> objects) where T : new()
        {
            Result result = GetDisplayAttributePropertiesByType(typeof(T), out List<string> headers, out List<string> props);
            if (!result)
                return result;

            StringBuilder builder = new();

            ApplyHeadersToStringBuilder(headers, ref builder);

            result = BuildText(objects, props, ref builder);
            if (!result)
                return result;

            result = await SaveFileFromText(builder.ToString());
            if (!result)
                return result;

            return true;
        }

        private static Result GetDisplayAttributePropertiesByType(Type type, out List<string> displayAttributes, out List<string> props)
        {
            var properties = type.GetProperties();
            displayAttributes = new();
            props = new();
            foreach (var property in properties)
            {
                Result result = GetDisplayAttributeForProperty(property, out string displayAttribute);
                if (!result)
                    return result;

                if (displayAttributes.Contains(displayAttribute))
                {
                    var match = displayAttributes.FirstOrDefault(h => h == displayAttribute);
                    return $"Duplicate Display Name \"{displayAttribute}\" in {type.Name}. {property.Name} matches {match}";
                }
                displayAttributes.Add(displayAttribute);
                props.Add(property.Name);
            }

            if (displayAttributes.Count == 0 || props.Count == 0)
                return $"{type.Name} does not have Properties with the DisplayAttribute";

            return true;
        }

        private static void ApplyHeadersToStringBuilder(List<string> headers, ref StringBuilder builder)
        {
            var count = headers.Count;
            for (int i = 0; i < count; i++)
            {
                AddValueToStringBuilder(headers[i], ref builder);

                ApplyEndCharacter(count, i, ref builder);
            }
        }

        private static Result BuildText<T>(List<T> objects, List<string> props, ref StringBuilder builder)
        {
            var objectCount = objects.Count;
            var propsCount = props.Count;
            for (int i = 0; i < objectCount; i++)
            {
                var obj = objects[i];
                
                for (int k = 0; k < propsCount; k++)
                {
                    var prop = props[k];

                    Result result = GetValueFromProperty(obj, prop, out object? value);
                    if (!result)
                        return result;

                    AddValueToStringBuilder(value, ref builder);

                    ApplyEndCharacter(propsCount, k, ref builder);
                }
            }
            return true;
        }

        private static Result GetValueFromProperty<T>(T obj, string prop, out object? value)
        {
            value = null;
            if (obj == null)
            {
                return $"Cannot export a null object.";
            }
            var info = obj.GetType().GetProperty(prop);

            if (info == null)
            {
                return $"Could not find PropertyInfo for {prop}";
            }

            value = info.GetValue(obj);
            return true;
        }

        private static void ApplyEndCharacter(int max, int current, ref StringBuilder builder)
        {
            if (current < max - 1)
                builder.Append(", ");
            else
                builder.AppendLine();
        }

        private static void AddValueToStringBuilder(object? value, ref StringBuilder builder)
        {
            if (value == null)
                return;

            var type = value.GetType();
            bool hasComma = false;

            if (type == typeof(string))
            {
                if (((string)value).Contains(','))
                {
                    builder.Append('\"');
                    hasComma = true;
                }
            }

            builder.Append(value);

            if (hasComma)
                builder.Append('\"');
        }

        private static async Task<Result> SaveFileFromText(string text)
        {
            try
            {
                /*
                 * Need to send this to the download location
                 */
                var assembly = Assembly.GetEntryAssembly();
                string? path;

                if (assembly != null)
                    path = Path.GetDirectoryName(assembly.Location);
                else
                    path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

                if (string.IsNullOrWhiteSpace(path))
                    return "Path on fileSystem cannot be obtained. Cannot save file.";

                string fileName = $"Export Data.csv";
                string fullPath = Path.Combine(path, fileName);

                await File.WriteAllTextAsync(fullPath, text);
            }
            catch (Exception e)
            {
                return e.Message;
            }

            return true;
        }
        #endregion

        private static Result GetDisplayAttributeForProperty(PropertyInfo property, out string displayAttribute)
        {
            displayAttribute = string.Empty;
            var attribute = property.GetCustomAttribute<DisplayAttribute>();
            if (attribute == null)
                return false;
            
            if (string.IsNullOrWhiteSpace(attribute.Name))
                return $"No Name on DisplayAttribute for {property.Name}";

            displayAttribute = attribute.Name;
            return true;
        }

        #region Import

        /*
         * Known Bug
         * If a column name is added, i.e custom attribute, to the CSV file that does not exist
         * within the object being created as output the file will not be parsed
         * 
         * Fix
         * Need to keep track of columns that are not within the class and skip those columns
         * This may also represent a missing DisplayAttribute on a property as well
         * 
         */

        /// <summary>
        /// Opens a CSV file and attempts to parse each line to an object and puts them into a List
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="path">Path to file to be parsed</param>
        /// <param name="objects">List of objects created</param>
        /// <returns><see cref="Result"/> Returns true with no message if parse was successful otherwise returns false with error message.</returns>
        public static Result Import<T>(string path, out List<T> objects) where T : new()
        {
            Result result;
            objects = new();

            result = ParseCSV(path, out List<string[]> objectsData);
            if (!result)
                return result;

            result = GetObjectProperties<T>(objectsData[0], out string[] props);
            if (!result)
                return result;

            result = CreateObjects(objectsData, props, ref objects);
            if (!result)
                return result;

            return true;
        }

        private static Result ParseCSV(string path, out List<string[]> objectsData)
        {
            using TextFieldParser parser = new(path)
            {
                CommentTokens = new string[] { "#" },
                HasFieldsEnclosedInQuotes = true,
                TrimWhiteSpace = true,
                TextFieldType = FieldType.Delimited
            };
            parser.SetDelimiters(new string[] { "," });

            objectsData = new();

            while (!parser.EndOfData)
            {
                string[]? fields = parser.ReadFields();

                if (fields == null)
                    return $"Could not Parse CSV from path {path}. Is the file empty?";

                objectsData.Add(fields);
            }

            parser.Close();

            if (objectsData.Count == 0)
                return "No objects were parsed from the file";

            var headers = objectsData[0];

            Result result = ValidateHeaders(headers);
            if (!result)
                return result;

            return true;
        }

        private static Result ValidateHeaders(string[] headers)
        {
            int length = headers.Length;

            var check = new string[length];

            for (int i = 0; i < length; i++)
            {
                var header = headers[i];

                if (string.IsNullOrWhiteSpace(header))
                    return "Empty header found in CSV file.";

                if (check.Contains(header))
                    return $"Found multiple headers using the same name in CSV file: {header}";

                check[i] = header;
            }
            return true;
        }

        /// <summary>
        /// Gets the Properties in sorted order based on CSV header order
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="headers">Array of headers from CSV file</param>
        /// <param name="props">Properties sorted by header order</param>
        /// <returns>Returns <see cref="Result.Value"/> true with no message on success
        /// <br/>
        /// Returns <see cref="Result.Value"/> false with error message on fail</returns>
        private static Result GetObjectProperties<T>(string[] headers, out string[] props)
        {
            int length = headers.Length;
            props = new string[length];

            Result result = GetPropertyData<T>(out Dictionary<string, string> listOfProperties);
            if (!result)
                return result;

            for (int i = 0; i < length; i++)
            {
                var header = headers[i];
                /*
                 * If header column exists but property does not exist on object this will fail
                 * Could fix this by not adding it to the list of properties and removing the header
                 */
                if (!listOfProperties.TryGetValue(header, out string? prop))
                    return $"\"{header}\" header found but DisplayAttribute missing on Property!";

                props[i] = prop;
            }

            return new Result(true, null);
        }

        /// <summary>
        /// Creates a Key Value Pair for the Header and Property
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="listOfProperties">Key Header Value Property Name</param>
        /// <returns>Returns <see cref="Result.Value"/> true with no message on success
        /// <br/>
        /// Returns <see cref="Result.Value"/> false with error message on fail</returns>
        private static Result GetPropertyData<T>(out Dictionary<string, string> listOfProperties)
        {
            var properties = typeof(T).GetProperties();
            listOfProperties = new();

            foreach (var property in properties)
            {
                Result result = GetDisplayAttributeForProperty(property, out string displayAttribute);
                if (!result)
                    return result;

                if (listOfProperties.ContainsKey(displayAttribute))
                {
                    listOfProperties.TryGetValue(displayAttribute, out string? match);
                    return $"Duplicate Display Name \"{displayAttribute}\" in {typeof(T).Name}. {property.Name} matches {match}";
                }

                listOfProperties.Add(displayAttribute, property.Name);
            }

            if (listOfProperties.Keys.Count == 0)
                return $"DisplayAttribute with Name not found on any properties in {typeof(T).Name}";

            return true;
        }

        private static Result CreateObjects<T>(List<string[]> objectsData, string[] props, ref List<T> objects) where T : new()
        {
            Result result;
            var objectsDataCount = objectsData.Count;
            for (int i = 1; i < objectsDataCount; i++)
            {
                result = CreateObject(objectsData[i], props, out T t);
                if (!result)
                    return result;

                objects.Add(t);
            }
            return true;
        }

        private static Result CreateObject<T>(string[] objectData, string[] props, out T t) where T : new()
        {
            t = new();

            /*
             * This will be thrown if the CSV has more columns then the object being created. Should search the object and see which header
             * is not in the object labeled with DisplayAttribute and skip that column. This check is not needed and the property should be
             * skipped. However there could be a programmer mistake where a DisplayAttribute was forgotten and if this is the case the 
             * export feature will not parse it.
             */

            if (objectData.Length > props.Length)
                return $"Column count is greater than Property count in {typeof(T).Name} using the DisplayAttribute";

            int length = objectData.Length;
            for (int i = 0; i < length; i++)
            {
                /*
                 * Property could be null checked here. If the property is null it means a header column was added to the CSV file
                 * that does not exist in the object
                 */
                Result result = SetProperty(props[i], ref t, objectData[i]);
                if (!result)
                    return result;
            }

            return true;
        }

        private static Result SetProperty<T>(string prop, ref T obj, string? value)
        {
            /*
             * Prop will be null when a header in the CSV does not have a DisplayAttribute associated with a property
             * on the object being created.
             */
            if (prop == null)
                return $"Property is null. It appears a column in the SCV does not exist with in the {typeof(T).Name} object";

            var info = typeof(T).GetProperty(prop);

            if (info == null)
                return $"Property {prop} from {obj?.GetType().Name} Not Found!";

            object? setValue;

            if (string.IsNullOrWhiteSpace(value))
                setValue = null;
            else
            {
                var typeConverter = TypeDescriptor.GetConverter(info.PropertyType);
                if (!typeConverter.IsValid(value))
                    return $"{value} cannot be converted to {info.PropertyType}";

                setValue = typeConverter.ConvertFromString(value);
            }

            info.SetValue(obj, setValue, null);

            return true;
        }

        #endregion
    }

    public struct Result
    {
        public Result(bool value, string? results)
        {
            Value = value;
            Results = results;
        }
        public readonly bool Value { get; }
        public readonly string? Results { get; }

        public static implicit operator Result(string? s)
        {
            if (s == null)
                return new Result(true, null);

            return new Result(false, s);
        }

        public static implicit operator Result(bool b)
        {
            if (!b)
                return new Result(b, "Failed");

            return new Result(b, null);
        }

        public static implicit operator bool(Result result)
        {
            return result.Value;
        }

        public static implicit operator string?(Result result)
        {
            if (result.Value)
                return null;

            return result.Results;
        }
    }
}
