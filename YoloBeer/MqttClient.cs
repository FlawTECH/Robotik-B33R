using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.ManagedClient;
using Newtonsoft.Json;

namespace YoloBeer
{
    public class MqttClient
    {
        private static IManagedMqttClient _mqttClientInstance;

        public static IManagedMqttClient MqttClientInstance
        {
            get
            {
                if (_mqttClientInstance != null)
                    return _mqttClientInstance;
                var factory = new MqttFactory();
                _mqttClientInstance = factory.CreateManagedMqttClient();
                _mqttClientInstance.ApplicationMessageReceived += _mqttClientInstance_ApplicationMessageReceived;
                _mqttClientInstance.Connected += _mqttClientInstance_Connected;
                return _mqttClientInstance;
            }
        }

        private static Dictionary<string, int> _drinks;
        private static string RequestedBeer { get; set; }
        private static bool IsArmCycling { get; set; }
        private static Dictionary<string, int> _lastSeenBeers;
        private static string GrabPos;

        static MqttClient()
        {
            _drinks = new Dictionary<string, int>();
            RequestedBeer = "";
            IsArmCycling = false;
            _lastSeenBeers = new Dictionary<string, int>();
        }

        private static void _mqttClientInstance_Connected(object sender, MqttClientConnectedEventArgs e)
        {
            MqttClientInstance.SubscribeAsync(new TopicFilterBuilder().WithTopic("php/drinks/+/+").Build());
            MqttClientInstance.SubscribeAsync(new TopicFilterBuilder().WithTopic("arm/reply/+").Build());

        }

        private static void _mqttClientInstance_ApplicationMessageReceived(object sender, MqttApplicationMessageReceivedEventArgs e)
        {
            var topicSubject = e.ApplicationMessage.Topic.Split('/')[0];
            switch (topicSubject)
            {
                case "php":
                    if (e.ApplicationMessage.Topic.ToLower() == "php/drinks/getall")
                    {
                        _lastSeenBeers = new Dictionary<string, int>();
                        AnalyzeCurrentBeers();
                    }
                    else
                    {
                        GrabPos = "0";
                        RequestedBeer = e.ApplicationMessage.Topic.Split('/')[3];
                        IsArmCycling = true;
                        Yolo.IsSearchingForBeer = true;
                        //Sending tags to MQTT Yolo
                        var yoloMessage = new MqttApplicationMessageBuilder()
                            .WithTopic($"yolo/cmd")
                            .WithPayload("scan")
                            .WithAtMostOnceQoS()
                            .Build();

                        MqttClientInstance.PublishAsync(yoloMessage);
                    }

                    break;
                case "arm":

                    var topicObjective = e.ApplicationMessage.Topic.Split('/')[2];
                    switch(topicObjective)
                    {
                        case "scan":
                            if (Yolo.IsProbing)
                            {
                                Yolo.IsProbing = false;

                                //Sending tags to MQTT Yolo
                                var yoloMessage = new MqttApplicationMessageBuilder()
                                    .WithTopic($"yolo/drinks/results")
                                    .WithPayload(JsonConvert.SerializeObject(_drinks))
                                    .WithAtMostOnceQoS()
                                    .Build();

                                MqttClientInstance.PublishAsync(yoloMessage);
                            }
                            else if (Yolo.IsSearchingForBeer)
                            {
                                Yolo.IsSearchingForBeer = false;


                                if (GrabPos != "0")
                                {
                                    //Sending tags to MQTT Yolo
                                    var yoloMessage = new MqttApplicationMessageBuilder()
                                        .WithTopic($"yolo/cmd")
                                        .WithPayload("moveto," + GrabPos)
                                        .WithAtMostOnceQoS()
                                        .Build();

                                    MqttClientInstance.PublishAsync(yoloMessage);

                                    //Sending tags to MQTT Yolo
                                    yoloMessage = new MqttApplicationMessageBuilder()
                                        .WithTopic($"yolo/cmd")
                                        .WithPayload("pickup")
                                        .WithAtMostOnceQoS()
                                        .Build();

                                    MqttClientInstance.PublishAsync(yoloMessage);
                                }
                            }

                            break;

                        case "body":
                            //if(GrabPos=="0")
                                GrabPos = Encoding.Default.GetString(e.ApplicationMessage.Payload);
                            break;

                    }

                    break;
            }
        }

        public static void StartClient()
        {
            var options = new ManagedMqttClientOptionsBuilder()
                .WithAutoReconnectDelay(TimeSpan.FromSeconds(10))
                .WithClientOptions(
                    new MqttClientOptionsBuilder()
                        .WithClientId("YoloClient")
                        .WithTcpServer("localhost", 1883)
                        .Build()
                )
                .Build();


            MqttClientInstance.StartAsync(options);
            Console.WriteLine($"MQTT client connected");
        }

        public static void StopClient()
        {
            MqttClientInstance.StopAsync();
        }

        public static void AnalyzeCurrentBeers()
        {
            _drinks = new Dictionary<string, int>();

            //Sending tags to MQTT Yolo
            var yoloMessage = new MqttApplicationMessageBuilder()
                .WithTopic($"yolo/cmd")
                .WithPayload("scan")
                .WithAtMostOnceQoS()
                .Build();

            MqttClientInstance.PublishAsync(yoloMessage);

            Yolo.IsProbing = true;
        }

        public static void ProcessEvents()
        {
            if (Yolo.IsProbing)
            {
                //TODO REPLACE IMAGE
                var detectionResults = Yolo.DetectObjects(CameraHelper.CaptureFrame());
                var valuesToAdd = new Dictionary<string, int>();
                foreach (var result in detectionResults)
                {

                    if (!valuesToAdd.ContainsKey(Yolo.GetClassnameById(result.ObjId)))
                        valuesToAdd.Add(Yolo.GetClassnameById(result.ObjId), 0);

                    valuesToAdd[Yolo.GetClassnameById(result.ObjId)]++;
                }

                var tmpLastValues = new Dictionary<string, int>();
                foreach (var entry in valuesToAdd)
                {
                    if (!_lastSeenBeers.ContainsKey(entry.Key))
                    {
                        if(!_drinks.ContainsKey(entry.Key))
                            _drinks.Add(entry.Key, 0);
                        _drinks[entry.Key] += entry.Value;
                        //Console.WriteLine("Added 1");
                    }
                    else if (_lastSeenBeers[entry.Key] > entry.Value)
                    {
                        if (!_drinks.ContainsKey(entry.Key))
                            _drinks.Add(entry.Key, 0);
                        _drinks[entry.Key] += entry.Value;
                        //Console.WriteLine("Added 2");
                    }

                    tmpLastValues.Add(entry.Key, entry.Value);

                    //Console.WriteLine("check routine done");

                    _lastSeenBeers = tmpLastValues;
                }



            }
            else if (Yolo.IsSearchingForBeer)
            {
                var camMat = CameraHelper.CaptureFrame();
                var detectionResults = Yolo.DetectObjects(camMat);

                foreach (var result in detectionResults)
                {
                    var imageHCenter = (camMat.Cols / 2)-110;

                    //if (result.X + (result.W / 2) - imageHCenter >= 0 && result.X + (result.W / 2) - imageHCenter < imageHCenter && result.X + (result.W / 2) - imageHCenter > result.W)
                    //{
                    //    if (IsArmCycling)
                    //    {
                    //        IsArmCycling = false;
                    //        //Sending stop cmd to arm
                    //        var yoloMessage = new MqttApplicationMessageBuilder()
                    //            .WithTopic($"yolo/cmd")
                    //            .WithPayload("stop")
                    //            .WithAtMostOnceQoS()
                    //            .Build();

                    //        MqttClientInstance.PublishAsync(yoloMessage);
                    //    }

                    //    //Moving left
                    //    var yoloCmd = new MqttApplicationMessageBuilder()
                    //        .WithTopic($"yolo/cmd")
                    //        .WithPayload("stepright")
                    //        .WithAtMostOnceQoS()
                    //        .Build();

                    //    MqttClientInstance.PublishAsync(yoloCmd);

                    //    Console.WriteLine("Move right");


                    //}
                    //else if (result.X + (result.W / 2) - imageHCenter <= 0 && result.X + (result.W / 2) - imageHCenter > (-imageHCenter) && result.X + (result.W / 2) - imageHCenter < (-result.W))
                    //{
                    //    if (IsArmCycling)
                    //    {
                    //        IsArmCycling = false;
                    //        //Sending stop cmd to arm
                    //        var yoloMessage = new MqttApplicationMessageBuilder()
                    //            .WithTopic($"yolo/cmd")
                    //            .WithPayload("stop")
                    //            .WithAtMostOnceQoS()
                    //            .Build();

                    //        MqttClientInstance.PublishAsync(yoloMessage);
                    //    }

                    //    //Moving right
                    //    var yoloCmd = new MqttApplicationMessageBuilder()
                    //        .WithTopic($"yolo/cmd")
                    //        .WithPayload("stepleft")
                    //        .WithAtMostOnceQoS()
                    //        .Build();

                    //    MqttClientInstance.PublishAsync(yoloCmd);

                    //    Console.WriteLine("Move left");


                    //}
                    if (result.X + (result.W / 2) - imageHCenter <= result.W && result.X + (result.W / 2) - imageHCenter >= (-result.W))
                    {
                        //On bottle
                        //if (IsArmCycling)
                        //{
                        //    IsArmCycling = false;
                        //    //Sending stop cmd to arm
                        //    var yoloMessage = new MqttApplicationMessageBuilder()
                        //        .WithTopic($"yolo/cmd")
                        //        .WithPayload("stop")
                        //        .WithAtMostOnceQoS()
                        //        .Build();

                        //    MqttClientInstance.PublishAsync(yoloMessage);
                        //}

                        //Pickup
                        //var yoloCmd = new MqttApplicationMessageBuilder()
                        //    .WithTopic($"yolo/cmd")
                        //    .WithPayload("pickup")
                        //    .WithAtMostOnceQoS()
                        //    .Build();

                        //MqttClientInstance.PublishAsync(yoloCmd);

                        //Yolo.IsSearchingForBeer = false;

                        var yoloCmd = new MqttApplicationMessageBuilder()
                            .WithTopic($"yolo/cmd")
                            .WithPayload("getbody")
                            .WithAtMostOnceQoS()
                            .Build();

                        MqttClientInstance.PublishAsync(yoloCmd);

                        Console.WriteLine("Found");

                    }
                }
            }
        }
    }
}
