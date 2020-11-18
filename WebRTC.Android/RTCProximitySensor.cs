using Android.Content;
using Android.Hardware;
using Android.Runtime;
using Java.Lang;
using Org.Webrtc;

namespace WebRTC.Android
{
    class RTCProximitySensor : Java.Lang.Object, ISensorEventListener
    {
        private Runnable _onSensorStateListener;
        private SensorManager _sensorManager;
        private Sensor _proximitySensor;
        private bool _lastStateReportIsNear;

        public static RTCProximitySensor Create(Context context, Runnable sensorStateListener)
        {
            return new RTCProximitySensor(context, sensorStateListener);
        }

        private RTCProximitySensor(Context context, Runnable sensorStateListener)
        {
            _onSensorStateListener = sensorStateListener;
            _sensorManager = SensorManager.FromContext(context);
        }

        private bool InitDefaultSensor()
        {
            ThreadUtils.CheckIsOnMainThread();
            if (_proximitySensor != null)
                return true;
            _proximitySensor = _sensorManager.GetDefaultSensor(SensorType.Proximity);
            if (_proximitySensor == null)
                return false;
            return true;
        }

        public bool Start()
        {
            ThreadUtils.CheckIsOnMainThread();
            if (!InitDefaultSensor())
                return false;
            _sensorManager.RegisterListener(this, _proximitySensor, SensorDelay.Normal);
            return true;
        }

        public void Stop()
        {
            ThreadUtils.CheckIsOnMainThread();
            if (_proximitySensor == null)
                return;
            _sensorManager.UnregisterListener(this, _proximitySensor);
        }

        public bool SensorReportNearState()
        {
            ThreadUtils.CheckIsOnMainThread();
            return _lastStateReportIsNear;
        }

        public void OnAccuracyChanged(Sensor sensor, [GeneratedEnum] SensorStatus accuracy) { }

        public void OnSensorChanged(SensorEvent e)
        {
            ThreadUtils.CheckIsOnMainThread();
            if (e.Sensor.Type == SensorType.Proximity)
            {
                float distance = e.Values[0];
                if (distance < _proximitySensor.MaximumRange)
                    _lastStateReportIsNear = true;
                else
                    _lastStateReportIsNear = false;

                if (_onSensorStateListener != null)
                    _onSensorStateListener.Run();
            }
        }
    }
}