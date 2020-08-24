﻿// onotseike@hotmail.comPaula Aliu
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Routing.Matching;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WebRTC.Classes;
using WebRTC.Enums;
using WebRTC.Signal.Server.Models;
using WebRTC.Signal.Server.Models.Complex;
using MediaConstraints = WebRTC.Signal.Server.Models.Complex.MediaConstraints;

namespace WebRTC.Signal.Server.Hubs
{
    public class WebRTCHub : Hub//<IHubClient>
    {
        #region Properties

        
        #endregion

        #region Hub Override Method(s)

        public override Task OnDisconnectedAsync(Exception exception)
        {
            HangUp();
            HubObjects.Clients.RemoveAll(client => client.ClientId.ToString() == Context.ConnectionId);
            BroadcastHubClients();

            return base.OnDisconnectedAsync(exception);
        }

        #endregion

        #region Function(s)

        public void JoinHub(string _username)
        {
            if (!HubObjects.Clients.Exists(client => client.ClientId.ToString() == Context.ConnectionId))
            {
                HubObjects.Clients.Add(new Client(Context.ConnectionId, _username));
            }
            

            BroadcastHubClients();
        }

        public Tuple<bool, string> AddClientToRoom(string _clientId, string _roomId, bool _isIntiator, int 
        _maxOccupancy = 2)
        {
            if (_isIntiator)
            {
                var _room = HubObjects.Rooms.FirstOrDefault(room => room.RoomId.ToString() == _roomId);
                var _client = HubObjects.Clients.FirstOrDefault(client => client.ClientId.ToString() == _clientId);
                if (_room == null)
                {
                    var _newRoom = new Room(_roomId, _maxOccupancy);
                    if (_client != null)
                    {
                        if (_client.InRoom)
                        {
                            HangUp();
                        }

                        _client.IsInitiator = true;
                        _newRoom.Occupants.Add(_client);
                        return Tuple.Create(true, _newRoom.RoomId.ToString());
                    }
                    else
                    {
                        JoinHub($"CLIENT_Anonymous");
                        _client = HubObjects.Clients.FirstOrDefault(client => client.ClientId.ToString() == Context
                        .ConnectionId);
                        
                        if (_client == null) return Tuple.Create<bool, string>(false, $"ERROR : Client is not connected to the Hub");

                        _client.IsInitiator = true;
                        _newRoom.Occupants.Add(_client);
                        return Tuple.Create(true, _newRoom.RoomId.ToString()); 
                    }
                }
                else
                {
                        var _newRoom = new Room(Guid.NewGuid().ToString(), _maxOccupancy );
                        if (_client != null)
                        {
                            if (_client.InRoom)
                            {
                                HangUp();
                            }

                            _client.IsInitiator = true;
                            _newRoom.Occupants.Add(_client);
                            return Tuple.Create(true, _newRoom.RoomId.ToString());
                        }
                        else
                        {
                            JoinHub($"CLIENT_Anonymous");
                            _client = HubObjects.Clients.FirstOrDefault(client => client.ClientId.ToString() == Context
                            .ConnectionId);
                        
                            if (_client == null) return Tuple.Create<bool, string>(false, $"ERROR : Client is not connected to the Hub");

                            _client.IsInitiator = true;
                            _newRoom.Occupants.Add(_client);
                            return Tuple.Create(true, _newRoom.RoomId.ToString()); 
                        }
                }
            }
            else
            {
                //TODO: Get Room by _roomId; 
                //TODO: If room exists: add Client to room 
                // As Participant and not initiator. provided room isn't fully occupied
            }
            return Tuple.Create<bool, string>(false, "ERROR");
        }

        public void CallHubClient(string _targetClientId)
        {
            var caller = HubObjects.Clients.SingleOrDefault(client => client.ClientId.ToString() == Context.ConnectionId);
            var callee = HubObjects.Clients.SingleOrDefault(client => client.ClientId.ToString() == _targetClientId);

            if (callee == null)
            {
                Clients.Caller.SendAsync("CallDeclined", _targetClientId, $"The use you called is not available.");
                //Clients.Caller.CallDeclined(_targetClientId, $"The use you called is not available.");
                return;
            }

            if (IsHubClientInRoom(callee.ClientId.ToString()) != null)
            {
                Clients.Caller.SendAsync("CallDeclined", _targetClientId,
                    $"The Client with ClientId: {_targetClientId} is already in a call.", callee.Username);
                //Clients.Caller.CallDeclined(_targetClientId, $"The Client with ClientId: {_targetClientId} is already in a call.", callee.Username);
                return;
            }

            //Clients.Client(_targetClientId).IncomingCall(caller.ClientId.ToString());

            if (caller.InRoom && caller.IsInitiator)
            {
                UpdateCallOffer(callee, caller);
            }
            else
            {
                HubObjects.CallOffers.Add(new CallOffer
                {
                    Initiator = caller,
                    Participants = new List<Client> { callee }
                });
            }


        }

        public void AnwerCall(bool _acceptCall, string _targetClientId, int _maxOccupancy = 2)
        {
            var caller = HubObjects.Clients.SingleOrDefault(client => client.ClientId.ToString() == Context.ConnectionId);
            var callee = HubObjects.Clients.SingleOrDefault(client => client.ClientId.ToString() == _targetClientId);

            // This can only happen if the server-side came down and clients were cleared, while the user
            // still held their browser session.
            if (caller == null) return;

            if (callee == null)
            {
                Clients.Caller.SendAsync("CallEnded",_targetClientId,$"The Participant with Caller ClientId : {_targetClientId}, has ) left the call");
                return;
            }

            if (!_acceptCall)
            {
                Clients.Client(_targetClientId).SendAsync("CallDeclined", callee.ClientId, $"Participant with ClientId: {_targetClientId} did not  accept your call.", callee.Username);
                return;
            }

            var callOfferCount = HubObjects.CallOffers.RemoveAll(offer => offer.Initiator.ClientId == caller.ClientId && offer
            .Participants.FirstOrDefault(client => client.ClientId == callee.ClientId) != null);
            if (callOfferCount < 1)
            {
                Clients.Caller.SendAsync("CallEnded",_targetClientId, $"Participant with Clientid: {_targetClientId} has already hung up.)", callee.Username);
                return;
            }

            if (IsHubClientInRoom(callee.ClientId.ToString()) != null)
            {
                Clients.Caller.SendAsync("CallDeclined",_targetClientId, $"Participant with ClientId: {_targetClientId} is currently ) engaged in another call.", callee.Username);
                return;
            }

            HubObjects.CallOffers.RemoveAll(offer => offer.Initiator.ClientId == caller.ClientId);

            var newRoom = new Room(Guid.NewGuid().ToString(), _maxOccupancy);
            newRoom.AddClients(UpdateClientStates(caller, callee));
            HubObjects.Rooms.Add(newRoom);

            Clients.Client(_targetClientId).SendAsync("CallAccepted", caller);

            BroadcastHubClients();

        }

        public void HangUp()
        {
            var caller = HubObjects.Clients.SingleOrDefault(client => client.ClientId.ToString() == Context.ConnectionId);
            if (caller == null) return;

            var room = IsHubClientInRoom(caller.ClientId.ToString());
            if (room != null)
            {
                foreach (var occupant in room.Occupants.Where(_occupant => _occupant.ClientId != caller.ClientId))
                {
                    Clients.Client(occupant.ClientId).SendAsync("CallEnded", caller.ClientId, $"Participant with ClientId: {caller.ClientId} has Hung up.", caller.Username);
                    //Clients.Client(occupant.ClientId).CallEnded(caller.ClientId, $"Participant with ClientId: {caller.ClientId} has Hung up.", caller.Username);
                }
                room.Occupants.RemoveAll(occupant => occupant.ClientId == caller.ClientId);
                if (room.Occupants.Count < 2)
                {
                    HubObjects.Rooms.RemoveAll(_room => _room.RoomId == room.RoomId);
                }
            }

            HubObjects.CallOffers.RemoveAll(offer => offer.Initiator.ClientId == caller.ClientId || offer.Participants.Any(participant => participant.ClientId == caller.ClientId));
            BroadcastHubClients();

        }

        public void SendSignal(string _signal, string _targetClientId)
        {
            var caller = HubObjects.Clients.SingleOrDefault(client => client.ClientId.ToString() == Context.ConnectionId);
            var callee = HubObjects.Clients.SingleOrDefault(client => client.ClientId.ToString() == _targetClientId);

            if (caller == null || callee == null) return;

            var room = IsHubClientInRoom(_targetClientId);

            if (room != null && room.Occupants.Exists(_occupant => _occupant.ClientId == callee.ClientId))
            {
                Clients.Client(_targetClientId).SendAsync("ReceiveSignal",caller, _signal);
            }

        }

        #endregion

        #region Helper Function(s)

        private Room IsHubClientInRoom(string _clientId) => HubObjects.Rooms.FirstOrDefault(room => room.Occupants.Any(occupant => 
            occupant.ClientId.ToString() == _clientId));

        private void BroadcastHubClients()
        {
            HubObjects.Clients.ForEach(client => client.InRoom = (IsHubClientInRoom(client.ClientId) != null));
            var _hubClients = JArray.FromObject(HubObjects.Clients);
            Clients.All.SendAsync("UpdateHubClientsList", _hubClients);
            //Clients.All.UpdateHubClientsList(_hubClients);
        }

        private CallOffer GetCallOffer(string _callerId) => HubObjects.CallOffers.SingleOrDefault(offer => offer.Initiator
        .ClientId.ToString() == _callerId);

        private Tuple<bool, string> UpdateCallOffer(Client _participant, Client _initiator)
        {
            var callOffer = GetCallOffer(_initiator.ClientId.ToString());
            if (callOffer != null)
            {
                HubObjects.CallOffers.Remove(callOffer);
                callOffer.Participants.Add(_participant);
                HubObjects.CallOffers.Add(callOffer);
                return Tuple.Create(true, $"Participant with ClientId: {_participant.ClientId} has been added");
            }
            return Tuple.Create(false, $"Participant with ClientId : {_participant.ClientId} could not be added");
        }

        private Tuple<Client, Client> UpdateClientStates(Client _caller, Client _callee)
        {
            _caller.InRoom = true;
            _caller.IsInitiator = true;
            _callee.InRoom = true;
            _callee.IsInitiator = false;
            return Tuple.Create(_caller, _callee);
        }

        private IceCandidate RecreateIceCandidate(string _json)
        {
            var jsonDict = JsonConvert.DeserializeObject<Dictionary<string, string>>(_json);
            if (jsonDict["type"].Equals("candidate"))
            {
                int.TryParse(jsonDict["label"], out int label);
                return new IceCandidate(jsonDict["candidate"], jsonDict["id"], label);
            }

            return null;
        }
        
        #endregion

        #region WebRTC Functions

        public async Task<string> GetRoomParametersAsync(string _roomId, bool _isInitiator, string _turnBaseUrl = 
        "https://global.xirsys.net/_turn/DemoWebRTC")
        {
            if (_isInitiator)
            {
                var addClientResponse = AddClientToRoom(Context.ConnectionId, _roomId, _isInitiator);
                if (addClientResponse.Item1)
                {
                    var _turnClient = new TURNClient(_turnBaseUrl,"Onotseike:37aac6f4-cf3d-11ea-9990-0242ac150003");
                    var roomParameters = new RoomParameters
                    {
                        RoomId = addClientResponse.Item2,
                        ClientId = Context.ConnectionId,
                        RoomUrl = "",
                        IsInitiator = _isInitiator,
                        MediaConstraints = new MediaConstraints { Audio = true, Video = true},
                        IceServerUrl = _turnBaseUrl,
                        IsLoopBack = false
                    };

                    var _iceServers = await _turnClient.RequestServersAsync();
                    roomParameters.IceServers = _iceServers;
                    roomParameters.OfferSdp = new SessionDescription(SessionDescription.GetSdpTypeFromString("offer"), "offer");
                    return JObject.FromObject(roomParameters).ToString();
                }
            }
            else
            {
                //TODO: Receive or Join A call
            }

            return null;
        }

        public string SendLocalIceCandidate(string _clientId, string _candidate)
        {
            var candidate = RecreateIceCandidate(_candidate);
            if (candidate == null)
            {
                return JObject.FromObject(Tuple.Create(false, $"SENDING ICE CANDIDATE ERROR: INVALID ICE CANDIDATE"))
                .ToString();
            }
            var client = HubObjects.Clients.FirstOrDefault(client => client.ClientId == _clientId);
            if (client == null)
            {
                return JObject.FromObject(Tuple.Create(false,
                    $"SENDING ICE CANDIDATE ERROR: CLIENT WITH ID: {_clientId} IS NOT CONNECTED TO THE HUB")).ToString();
            }

            client.Candidate.Add(candidate);
            HubObjects.Clients.RemoveAll(client => client.ClientId == _clientId);
            HubObjects.Clients.Add(client);

            if (client.InRoom)
            {
                foreach (var room in HubObjects.Rooms)
                {
                    var isUpdated = room.UpdateClient(client);
                    if (isUpdated.Item1)
                    {
                        return JObject.FromObject(Tuple.Create(true, $"CLIENT WITH ID {_clientId} HAS UPDATED IT'S  ICE CANDIDATE")).ToString();
                    }
                }
                
            }
            
            return JObject.FromObject(Tuple.Create(false, $"CLIENT WITH ID {_clientId} HAS NOT UPDATED IT'S ICE CANDIDATE"))
            .ToString();
        }

        #endregion
    }
}
