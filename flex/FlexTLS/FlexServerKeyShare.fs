(*
 * Copyright 2015 INRIA and Microsoft Corporation
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *     http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 *)

#light "off"

/// <summary>
/// Module receiving, sending and forwarding TLS Server Key Share messages.
/// </summary>
module FlexTLS.FlexServerKeyShare

open NLog

open Bytes
open Error
open TLSInfo
open TLSConstants
open TLSExtensions
open HandshakeMessages

open FlexTypes
open FlexSecrets
open FlexConstants
open FlexHandshake

/// <summary>
/// Module receiving, sending and forwarding TLS Server Key Share messages.
/// </summary>
type FlexServerKeyShare =
    class

    /// <summary>
    /// EXPERIMENTAL TLS 1.3 Receive DHE ServerKeyExchange from the network stream
    /// </summary>
    /// <param name="st"> State of the current Handshake </param>
    /// <param name="nsc"> Next security context that embed the kex to be sent </param>
    /// <returns> Updated state * FServerKeyExchange message record </returns>

    static member receive (st:state, nsc:nextSecurityContext) : state * nextSecurityContext * FServerKeyShare =
        let nsckex13 =
            match nsc.secrets.kex with
            | DH13(dh13) -> dh13
            | _ -> failwith (perror __SOURCE_FILE__ __LINE__  "key exchange parameters has to be DH13")
        in
        let st,fsks = FlexServerKeyShare.receive(st,nsckex13.group) in
        let gy =
            match fsks.kex with
            | DHE(_,gy) -> gy
        in
        let kex13 = { nsckex13 with gy = gy } in
        let epk = {nsc.secrets with kex = DH13(kex13) } in
        let nsc = {nsc with secrets = epk} in
        let nsc = FlexSecrets.fillSecrets(st,Client,nsc) in
        st,nsc,fsks

    /// <summary>
    /// EXPERIMENTAL TLS 1.3 Receive DHE ServerKeyExchange from the network stream
    /// </summary>
    /// <param name="st"> State of the current Handshake </param>
    /// <param name="group"> DH group negotiated and received in ServerHello </param>
    /// <returns> Updated state * FServerKeyExchange message record </returns>

    static member receive (st:state, group:dhGroup) : state * FServerKeyShare =
        LogManager.GetLogger("file").Info("# SERVER KEY SHARE : FlexServerKeyShare.receive");
        let st,hstype,payload,to_log = FlexHandshake.receive(st) in
        match hstype with
        | HT_server_key_exchange  ->
            (match HandshakeMessages.parseTLS13SKEDHE group payload with
            | Error (_,x) -> failwith (perror __SOURCE_FILE__ __LINE__ x)
            | Correct (kex) ->
                let fsks : FServerKeyShare = { kex = kex; payload = to_log } in

                let group,gy =
                    match kex with
                    | DHE(group,gy) -> group,gy
                in
                LogManager.GetLogger("file").Debug(sprintf "--- Public group : %A" group);
                LogManager.GetLogger("file").Debug(sprintf "--- Public DHE Exponent : %s" (Bytes.hexString(gy)));
                LogManager.GetLogger("file").Debug(sprintf "--- Payload : %s" (Bytes.hexString(payload)));
                st,fsks
            )
        | _ -> failwith (perror __SOURCE_FILE__ __LINE__ (sprintf "Unexpected handshake type: %A" hstype))

    /// <summary>
    /// EXPERIMENTAL TLS 1.3 Overload : Send a DHE ServerKeyExchange message to the network stream
    /// </summary>
    /// <param name="st"> State of the current Handshake </param>
    /// <param name="nsc"> Next security context that embed the kex to be sent </param>
    /// <param name="fp"> Optional fragmentation policy at the record level </param>
    /// <returns> Updated state * FServerKeyExchangeTLS13 message record </returns>
    static member send (st:state, nsc:nextSecurityContext, ?fp:fragmentationPolicy) : state * nextSecurityContext * FServerKeyShare =
        let fp = defaultArg fp FlexConstants.defaultFragmentationPolicy in
        let kex =
            match nsc.secrets.kex with
            | DH13(kex) when not (kex.gx = empty_bytes) ->
                // User-provided kex; don't alter it
                nsc.secrets.kex
            | _ ->
                // User didn't provide any useful default:
                // We sample DH parameters, and get client share from its offers
                let group =
                    match getNegotiatedDHGroup nsc.si.extensions with
                    | None -> failwith (perror __SOURCE_FILE__ __LINE__ "TLS 1.3 requires a negotiated DH group")
                    | Some(group) -> group
                in
                // pick client offer
                match List.tryFind
                    (fun x -> match x with
                        | DH13(x) -> x.group = group
                        | _ -> false ) nsc.offers with
                | None -> failwith (perror __SOURCE_FILE__ __LINE__ "Client provided no suitable offer")
                | Some(offer) ->
                    match offer with
                    | DH13(offer) ->
                        let x,gx = CoreDH.gen_key (dhgroup_to_dhparams offer.group) in
                        DH13({offer with x = x; gx = gx})
                    | _ -> failwith (perror __SOURCE_FILE__ __LINE__ "Unimplemented or unsupported key exchange")
        in

        let kex13 =
            match kex with
            | DH13(offer) ->
                DHE(offer.group,offer.gx)
            | _ -> failwith (perror __SOURCE_FILE__ __LINE__ "Unimplemented or unsupported key exchange")
        in
        let st,fsks = FlexServerKeyShare.send(st,kex13,fp) in
        let epk = {nsc.secrets with kex = kex } in
        let nsc = {nsc with secrets = epk} in
        let nsc = FlexSecrets.fillSecrets (st,Server,nsc) in
        st,nsc,fsks

    /// <summary>
    /// EXPERIMENTAL TLS 1.3 Send a DHE ServerKeyExchange message to the network stream
    /// </summary>
    /// <param name="st"> State of the current Handshake </param>
    /// <param name="dhgroup"> Diffie-Hellman negotiated group </param>
    /// <param name="fp"> Optional fragmentation policy at the record level </param>
    /// <returns> Updated state * FServerKeyExchangeTLS13 message record </returns>
    static member send (st:state, kex:tls13kex, ?fp:fragmentationPolicy) : state * FServerKeyShare =
        LogManager.GetLogger("file").Info("# SERVER KEY SHARE : FlexServerKeyShare.send");
        let fp = defaultArg fp FlexConstants.defaultFragmentationPolicy in

        let payload = HandshakeMessages.tls13SKEBytes kex in

        let st = FlexHandshake.send(st,payload,fp) in
        let fsks : FServerKeyShare = { kex = kex ; payload = payload } in

        let group,gx =
            match kex with
            | DHE(group,gx) -> group,gx
        in
        LogManager.GetLogger("file").Debug(sprintf "--- Public group : %A" group);
        LogManager.GetLogger("file").Debug(sprintf "--- Public DHE Exponent : %s" (Bytes.hexString(gx)));
        st,fsks

    end
