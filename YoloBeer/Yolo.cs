using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenCvSharp;
using YoloWrapper;

namespace YoloBeer
{
    class Yolo
    {
        private static string YoloCfg { get; set; }
        private static string YoloWeights { get; set; }
        private static string YoloNames { get; set; }
        private static Darknet _yoloDnnPicker;
        public static bool IsProbing { get; set; }
        public static bool IsSearchingForBeer { get; set; }

        private static string[] _loadedNames;

        public static void InitDnn(string yoloCfg, string yoloWeights, string yoloNames, int cudaGpuId)
        {
            YoloCfg = yoloCfg;
            YoloWeights = yoloWeights;
            YoloNames = yoloNames;
            _yoloDnnPicker = new Darknet(YoloCfg, YoloWeights);

            _loadedNames = File.ReadAllLines(YoloNames);
            IsProbing = false;
            IsSearchingForBeer = false;
        }

        public static List<NetResult> DetectObjects(Mat image)
        {
            return _yoloDnnPicker.Detect(image);
        }

        public static string GetClassnameById(uint id)
        {
            return _loadedNames[id];
        }

        public static Mat DrawBoxesOnImage(Mat image, List<NetResult> detectionResults)
        {
            foreach (var result in detectionResults)
            {

                //Formatting labels
                var label = $"{_loadedNames[result.ObjId]} {result.Prob * 100:0.00}%";

                //Determining label size & position
                //Uncomment to get text size
                /*var textSize = */
                Cv2.GetTextSize(
                    label,
                    HersheyFonts.HersheyTriplex,
                    0.5,
                    1,
                    out var baseline);

                //Drawing bbox
                Cv2.Rectangle(
                    image,
                    new OpenCvSharp.Point(result.X, result.Y),
                    new OpenCvSharp.Point(result.X + result.W, result.Y + result.H),
                    Scalar.DodgerBlue,
                    3);

                //Adding label on bbox
                Cv2.PutText(
                        image,
                        label, new OpenCvSharp.Point(result.X, result.Y - baseline),
                        HersheyFonts.HersheyTriplex,
                        0.5,
                        Scalar.Black);
            }

            return image;
        }
    }
}
