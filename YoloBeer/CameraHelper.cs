using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenCvSharp;

namespace YoloBeer
{
    public class CameraHelper
    {
        private static VideoCapture _cam;

        public static VideoCapture Cam
        {
            get
            {
                if (_cam != null)
                    return _cam;
                _cam = new VideoCapture(1);
                return _cam;
            }
            set { _cam = value; }
        }

        static CameraHelper()
        {

        }

        public static Mat CaptureFrame()
        {
            
            var matOut = new Mat();
            var mat = Cam.RetrieveMat();
            Point2f pt = new Point2f(mat.Cols / 2f, mat.Rows / 2f);
            var r = OpenCvSharp.Cv2.GetRotationMatrix2D(pt, 90f, 1.0);
            OpenCvSharp.Cv2.WarpAffine(mat, matOut, r, new Size(mat.Cols, mat.Rows));
            //OpenCvSharp.Cv2.Transpose(mat, matOut);
            //OpenCvSharp.Cv2.Flip(matOut, matOut, FlipMode.XY);
            //Cv2.ImShow("oui", matOut);
            //Cv2.WaitKey();
            return matOut;
        }


    }
}
