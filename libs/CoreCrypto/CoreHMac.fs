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

module CoreHMac
open Bytes
open CryptoProvider

type engine = HMac of CryptoProvider.HMac
type key    = bytes

let name (HMac engine) =
    engine.Name

let mac (HMac engine) (b : bytes) =
    abytes (engine.Process(cbytes b))

let md5engine    (k : key) = HMac (CoreCrypto.HMac "MD5"    (cbytes k))
let sha1engine   (k : key) = HMac (CoreCrypto.HMac "SHA1"   (cbytes k))
let sha256engine (k : key) = HMac (CoreCrypto.HMac "SHA256" (cbytes k))
let sha384engine (k : key) = HMac (CoreCrypto.HMac "SHA384" (cbytes k))
let sha512engine (k : key) = HMac (CoreCrypto.HMac "SHA512" (cbytes k))

let dohmac (factory : key -> engine) (k : key) (data : bytes) =
    mac (factory k) data

let md5    (k : key) (data : bytes) = dohmac md5engine    k data
let sha1   (k : key) (data : bytes) = dohmac sha1engine   k data
let sha256 (k : key) (data : bytes) = dohmac sha256engine k data
let sha384 (k : key) (data : bytes) = dohmac sha384engine k data
let sha512 (k : key) (data : bytes) = dohmac sha512engine k data
