using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using log4net;
using ServiceStack.Text;
using RabbitMQ.Client;
using System.Configuration;

namespace RabbitMQTools
{
    public static class RabbitJSONConfigurator
    {
        private static readonly ILog Log = LogManager.GetLogger("CommonLogger");
        private static ConnectionFactory rmqConnectionfactory;

        /// <summary>
        /// RabbitMQ exchanges and queues configuration from config object
        /// </summary>
        /// <param name="config">Configuration object</param>
        /// <param name="connectionString">For example: amqp://username:password@hostname:5672/virtualHostName</param>
        public static void Configure(Config config, string connectionString = null)
        {
            rmqConnectionfactory = connectionString == null ? 
                GetRMQConnection() : GetRMQConnection(connectionString);

            IConnection rabbitConnection = rmqConnectionfactory.CreateConnection();
            IModel rabbitChannel = rabbitConnection.CreateModel();
            IBasicProperties rabbitProperties;

            foreach (var exchangeConfig in config.Exchanges)
            {
                Dictionary<string, object> arguments = GetArguments(exchangeConfig.Arguments);

                Log.InfoFormat("Create Exchange {0}", exchangeConfig.Name);
                rabbitChannel.ExchangeDeclare(exchangeConfig.Name, 
                    exchangeConfig.Type.ToString(), 
                    exchangeConfig.Durability == DurabilityType.Durable ? true : false,
                    exchangeConfig.AutoDelete,
                    arguments);
            }

            foreach (var queueConfig in config.Queues)
            {
                Dictionary<string, object> arguments = GetArguments(queueConfig.Arguments);

                Log.InfoFormat("Create queue {0}", queueConfig.Name);
                rabbitChannel.QueueDeclare(queueConfig.Name, 
                    queueConfig.Durability == DurabilityType.Durable ? true : false, 
                    false, 
                    queueConfig.AutoDelete, 
                    arguments);

                foreach (var binding in queueConfig.Bindings)
                {
                    Dictionary<string, object> bindingArguments = GetArguments(binding.Arguments);
                    Log.InfoFormat("Binding queue {0} to Exchange {1}", queueConfig.Name, binding.FromExchange);
                    rabbitChannel.QueueBind(queueConfig.Name, binding.FromExchange, binding.RoutingKey, bindingArguments);
                }
            }

            rabbitProperties = rabbitChannel.CreateBasicProperties();
            rabbitProperties.Persistent = true;
            rabbitConnection.Close();
            Log.Info("Configuration complete");
        }
        /// <summary>
        /// RabbitMQ exchanges and queues configuration from json config file
        /// </summary>
        /// <param name="jsonPath">Path to JSON file</param>
        /// <param name="connectionString">For example: amqp://username:password@hostname:5672/virtualHostName</param>
        public static void Configure(string jsonPath = "rabbit-config.json", string connectionString = null)
        {
            try
            {
                string configJson = File.ReadAllText(jsonPath);
                Config config = JsonSerializer.DeserializeFromString<Config>(configJson);

                Configure(config, connectionString);
            }
            catch (FileNotFoundException ex)
            {
                Log.ErrorFormat("RabbitMQ configuration failed. File {0} not found.", jsonPath);
                throw ex;
            }
            catch(RabbitMQ.Client.Exceptions.OperationInterruptedException ex)
            {
                Log.Error("RabbitMQ configuration failed. Check JSON config");
                throw ex;
            }
            catch (Exception ex)
            {
                Log.Error("RabbitMQ configuration failed. Unhandled Exception");
                throw ex;
            }
        }

        private static Dictionary<string, object> GetArguments(List<Argument> argumentList)
        {
            Dictionary<string, object> arguments = new Dictionary<string, object>();

            foreach (var argument in argumentList)
            {
                object argValue = "undefined";
                switch (argument.Type)
                {
                    case DataType.String:
                        argValue = argument.Value;
                        break;
                    case DataType.Number:
                        argValue = int.Parse(argument.Value);
                        break;
                    case DataType.Boolean:
                        argValue = bool.Parse(argument.Value);
                        break;
                    case DataType.List:
                        argValue = GetListArguments(argument.ListArgumentItems);
                        break;
                    default:
                        break;
                }
                arguments.Add(argument.Name, argValue);
            }

            return arguments;
        }
        private static List<object> GetListArguments(List<ListArgumentItem> listArgumentsItems)
        {
            List<object> listArguments = new List<object>();

            foreach (var listArgumentsItem in listArgumentsItems)
            {
                object argValue = "undefined";
                switch (listArgumentsItem.Type)
                {
                    case DataType.String:
                        argValue = listArgumentsItem.Value;
                        break;
                    case DataType.Number:
                        argValue = int.Parse(listArgumentsItem.Value);
                        break;
                    case DataType.Boolean:
                        argValue = bool.Parse(listArgumentsItem.Value);
                        break;
                    case DataType.List:
                        argValue = GetListArguments(listArgumentsItem.ListArgumentItems);
                        break;
                    default:
                        break;
                }
                listArguments.Add(argValue);
            }

            return listArguments;
        }
        private static ConnectionFactory GetRMQConnection()
        {
            string Uri = ConfigurationManager.ConnectionStrings["RabbitMQ"].ConnectionString;
            return GetRMQConnection(Uri);
        }
        private static ConnectionFactory GetRMQConnection(string uri)
        {
            return new ConnectionFactory() { Uri = uri };
        }
    }

    /// <summary>
    /// Configuration object for RabbitJSONConfigurator
    /// </summary>
    public class Config
    {
        public List<Exchange> Exchanges { get; set; }
        public List<Queue> Queues { get; set; }
    }
    /// <summary>
    /// Object for RabbitMQ exchange description
    /// </summary>
    public class Exchange
    {
        public string Name { get; set; }
        public ExchangeType Type { get; set; }
        public DurabilityType Durability { get; set; }
        public bool AutoDelete { get; set; }
        public bool Internal { get; set; }
        public List<Argument> Arguments { get; set; }
    }
    /// <summary>
    /// Object for RabbitMQ queue description
    /// </summary>
    public class Queue
    {
        public string Name { get; set; }
        public DurabilityType Durability { get; set; }
        public bool AutoDelete { get; set; }
        public List<Argument> Arguments { get; set; }
        public List<Binding> Bindings { get; set; }
    }

    /// <summary>
    /// Object for argument (for exchange or queue) description
    /// </summary>
    public class Argument
    {
        public string Name { get; set; }
        public string Value { get; set; }
        public DataType Type { get; set; }
        public List<ListArgumentItem> ListArgumentItems { get; set; }
    }

    /// <summary>
    /// Object for List (type of argument) item description
    /// </summary>
    public class ListArgumentItem
    {
        public string Value { get; set; }
        public DataType Type { get; set; }
        public List<ListArgumentItem> ListArgumentItems { get; set; }
    }

    /// <summary>
    /// Queue binding description
    /// </summary>
    public class Binding
    {
        public string FromExchange { get; set; }
        public string RoutingKey { get; set; }
        public List<Argument> Arguments { get; set; }
    }

    public enum ExchangeType
    {
        fanout,
        direct,
        headers,
        topic
    }

    public enum DurabilityType
    {
        Durable,
        Transient
    }

    public enum DataType
    {
        String,
        Number,
        Boolean,
        List
    }

    public static class RabbitConstants
    {
        public static class ExchangeArguments
        {
            /// <summary>
            /// Alternate exchange
            /// </summary>
            public const string AlternateExchange = "alternate-exchange";
        }
        public static class QueueArguments
        {
            /// <summary>
            /// Message TTL (Number)
            /// </summary>
            public const string AlternateExchange = "x-message-ttl";
            /// <summary>
            /// Auto expire (String)
            /// </summary>
            public const string AutoExpire = "x-expires";
            /// <summary>
            /// Max length (Number)
            /// </summary>
            public const string MaxLength = "x-max-length";
            /// <summary>
            /// Max lenght bytes (Number)
            /// </summary>
            public const string MaxLenghtBytes = "x-max-length-bytes";
            /// <summary>
            /// Dead letter exchange (String)
            /// </summary>
            public const string DeadLetterExchange = "x-dead-letter-exchange";
            /// <summary>
            /// Dead letter routing key (String)
            /// </summary>
            public const string DeadLetterRoutingKey = "x-dead-letter-routing-key";
            /// <summary>
            /// Maximun priority (Number)
            /// </summary>
            public const string MaximunPriority = "x-max-priority";
        }

    }
}
