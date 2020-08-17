﻿// onotseike@hotmail.comPaula Aliu
using System;
using System.Threading.Tasks;

using Microsoft.AspNetCore.SignalR.Client;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using WebRTC.Classes;
using WebRTC.DemoApp.SignalRClient.Abstraction;
using WebRTC.DemoApp.SignalRClient.Responses;
using WebRTC.Enums;

namespace WebRTC.DemoApp.SignalRClient
{
    public class SRTCClient : IRTCClient<RoomConnectionParameters>
    {
        #region MessageType Enum

        private enum MessageType
        {
            Message,
            Leave
        }

        #endregion

        #region Properties & Variables

        private const string TAG = nameof(SRTCClient);

        private readonly ISignalingEvents<SignalingParameters> signalingEvents;
        private readonly IExecutorService executor;
        private readonly ILogger logger;

        private RoomConnectionParameters roomConnectionParameters;
        private bool isInitiator;
        private string messageUrl;


        #endregion

        #region Constructor(s)

        public SRTCClient(ISignalingEvents<SignalingParameters> _signalingEvents, ILogger _logger = null)
        {
            signalingEvents = _signalingEvents;
            executor = ExecutorServiceFactory.CreateExecutorService(nameof(SRTCClient));
            logger = _logger ?? new ConsoleLogger();

            State = ConnectionState.New;
        }

        #endregion

        #region Helper Function(s)

        private async void ConnectToHubRoom()
        {
            //TODO: Call SignalR Hub Connect To Room; This returns The relevant Room Parameters and potential errors if available.
            //await App.HubConnection.StartAsync();
            var str = await App.HubConnection.InvokeAsync<string>("GetRoomParametersAsync", roomConnectionParameters.RoomId, true, "https://global.xirsys.net/_turn/DemoWebRTC");
            var roomSObject = JObject.Parse(str);
            var roomParams = roomSObject.ToObject<RoomParameterResponse>();
            var _roomSignalingParameters = new SignalingParameters
            {
                IsInitiator = roomParams.IsInitiator,
                ClientId = roomParams.ClientId,
                IceServers = roomParams.IceServers,
                IceCandidates = roomParams.IceCandidates

            };
            executor.Execute(() =>
            {
                if (_roomSignalingParameters != null)
                {
                    ReportError($"ERROR");
                    return;
                }
                SignalingParametersReady(_roomSignalingParameters);
            });
        }

        private void SignalingParametersReady(SignalingParameters _roomSignalingParameters)
        {
            logger.Debug(TAG, "Room connection completed.");

            if (roomConnectionParameters.IsLoopback &&
                (!_roomSignalingParameters.IsInitiator || _roomSignalingParameters.OfferSdp != null))
            {
                ReportError("Loopback room is busy.");
                return;
            }

            if (!roomConnectionParameters.IsLoopback && !_roomSignalingParameters.IsInitiator &&
                _roomSignalingParameters.OfferSdp == null)
            {
                logger.Warning(TAG, "No offer SDP in room response.");
            }

            isInitiator = _roomSignalingParameters.IsInitiator;
            signalingEvents.OnChannelConnected(_roomSignalingParameters);
        }

        private void DisconnectFromHubRoom()
        {
            logger.Debug(TAG, "Disconnect. Room state: " + State);
            if (App.HubConnection.State == HubConnectionState.Connected)
            {
                logger.Debug(TAG, $"Leaving Room with RoomID: {roomConnectionParameters.RoomId}");
                //TODO: Call SignalR Leave Room Function
            }
            executor.Release();
        }

        private void ReportError(string _errorMessage)
        {
            logger.Error(TAG, _errorMessage);
            executor.Execute(() =>
            {
                if (State == ConnectionState.Error)
                    return;
                State = ConnectionState.Error;
                signalingEvents.OnChannelError(_errorMessage);
            });
        }

        private void SendPostMessage(MessageType _messageType, string _url, string _message)
        {
            var logInfo = _url;

            if (_message != null)
                logInfo += $". Message: {_message}";

            logger.Debug(TAG, $"C->GAE: {logInfo}");

            //var httpConnection = new AsyncHttpURLConnection(MethodType.Post, url, message, ((response, errorMessage) =>
            //{
            //    _executor.Execute(() =>
            //    {
            //        if (errorMessage != null)
            //        {
            //            ReportError($"GAE POST error : {errorMessage}");
            //            return;
            //        }

            //        if (messageType != MessageType.Message)
            //            return;
            //        try
            //        {
            //            var msg = JsonConvert.DeserializeObject<MessageResponse>(response);
            //            if (msg.Type != MessageResultType.Success)
            //            {
            //                ReportError($"GAE POST error : {response}");
            //            }
            //        }
            //        catch (JsonException e)
            //        {
            //            ReportError($"GAE POST JSON error: {e.Message}");
            //        }
            //    });
            //}));
            //httpConnection.Send();
        }


        #endregion

        #region IRTCClient Implementations

        public ConnectionState State { get; private set; }

        public void Connect(RoomConnectionParameters _connectionParameters)
        {
            roomConnectionParameters = _connectionParameters;
            executor.Execute(ConnectToHubRoom);
        }

        public void Disconnect() => executor.Execute(DisconnectFromHubRoom);

        public void SendOfferSdp(SessionDescription _sessionDescriotion)
        {
            executor.Execute(() =>
            {
                if (State != ConnectionState.Connected)
                {
                    ReportError("Sending offer SDP in non connected state.");
                    return;
                }

                var json = SignalingMessage.CreateJson(_sessionDescriotion);

                //SendPostMessage(MessageType.Message, messageUrl, json);
                //TODO : SignalR SDPOffer Method

                if (roomConnectionParameters.IsLoopback)
                {
                    // In loopback mode rename this offer to answer and route it back.
                    var sdpAnswer = new SessionDescription(SdpType.Answer, _sessionDescriotion.Sdp);
                    signalingEvents.OnRemoteDescription(sdpAnswer);
                }
            });
        }

        public void SendLocalIceCandidate(IceCandidate _candidate)
        {
            executor.Execute(() =>
            {
                var json = SignalingMessage.CreateJson(_candidate);
                if (isInitiator)
                {
                    if (State != ConnectionState.Connected)
                    {
                        ReportError("Sending ICE candidate in non connected state.");
                        return;
                    }

                    //SendPostMessage(MessageType.Message, _messageUrl, json);
                    //TODO: SignalR Send LocalIceCandidate as  Initiator 
                }
                else
                {
                    //_wsClient.Send(json);
                    //TODO: SignalR Send LocalIceCandidate as  Participant 
                }
            });
        }

        public void SendLocalIceCandidateRemovals(IceCandidate[] _candidates)
        {
            executor.Execute(() =>
            {
                var json = SignalingMessage.CreateJson(_candidates);

                if (isInitiator)
                {
                    if (State != ConnectionState.Connected)
                    {
                        ReportError("Sending ICE candidate removals in non connected state.");
                        return;
                    }

                    //SendPostMessage(MessageType.Message, _messageUrl, json);
                    //TODO: SignalR Send message to Remove Ice Candidates as Initiator
                    if (roomConnectionParameters.IsLoopback)
                    {
                        signalingEvents.OnRemoteIceCandidatesRemoved(_candidates);
                    }
                }
                else
                {
                    //_wsClient.Send(json);
                    //TODO: SignalR Send message to Remove Ice Candidates as Participant
                }
            });
        }

        public void SendAnswerSdp(SessionDescription _sessionDescription)
        {
            executor.Execute(() =>
            {
                if (roomConnectionParameters.IsLoopback)
                {
                    logger.Error(TAG, "Sending answer in loopback mode.");
                    return;
                }

                var json = SignalingMessage.CreateJson(_sessionDescription);


                //_wsClient.Send(json);
                //TODO: SignalR SSPAnswer Method
            });
        }

        #endregion


        #region SignalR Client Function(s)

        public void OnWebSocketClose() => signalingEvents.OnChannelClose();

        public void OnWebSocketMessage(string _message)
        {
            // Check HubConnection is Registered Connected
            if (App.HubConnection.State != HubConnectionState.Connected)
            {
                logger.Error(TAG, $"The SignalR HubConnection is in a Non-Connected State");
                return;
            }

            var _signalingMessage = SignalingMessage.MessageFromJSONString(_message);

            switch (_signalingMessage.Type)
            {
                case SignalingMessageType.Candidate:
                    var candidate = (ICECandidateMessage)_signalingMessage;
                    signalingEvents.OnRemoteIceCandidate(candidate.Candidate);
                    break;
                case SignalingMessageType.CandidateRemoval:
                    var candidates = (ICECandidateRemovalMessage)_signalingMessage;
                    signalingEvents.OnRemoteIceCandidatesRemoved(candidates.Candidates);
                    break;
                case SignalingMessageType.Offer:
                    if (!isInitiator)
                    {
                        var sdp = (SessionDescriptionMessage)_signalingMessage;
                        signalingEvents.OnRemoteDescription(sdp.Description);
                    }
                    else
                    {
                        ReportError($"Received offer for call receiver : {_message}");
                    }
                    break;
                case SignalingMessageType.Answer:
                    if (isInitiator)
                    {
                        var sdp = (SessionDescriptionMessage)_signalingMessage;
                        signalingEvents.OnRemoteDescription(sdp.Description);
                    }
                    else
                    {
                        ReportError($"Received answer for call initiator: {_message}");
                    }
                    break;
                case SignalingMessageType.Bye:
                    signalingEvents.OnChannelClose();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public void OnWebSocketError(string _errorDescription) => ReportError($"SignalR WebSocket Error: {_errorDescription}");

        #endregion

    }
}