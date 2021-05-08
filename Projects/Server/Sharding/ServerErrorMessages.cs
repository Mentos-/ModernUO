#region license

// Copyright (c) 2021, andreakarasho
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 1. Redistributions of source code must retain the above copyright
//    notice, this list of conditions and the following disclaimer.
// 2. Redistributions in binary form must reproduce the above copyright
//    notice, this list of conditions and the following disclaimer in the
//    documentation and/or other materials provided with the distribution.
// 3. All advertising materials mentioning features or use of this software
//    must display the following acknowledgement:
//    This product includes software developed by andreakarasho - https://github.com/andreakarasho
// 4. Neither the name of the copyright holder nor the
//    names of its contributors may be used to endorse or promote products
//    derived from this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS ''AS IS'' AND ANY
// EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

#endregion

using System;

namespace LoadTestUO
{
    internal static class ServerErrorMessages
    {
        private static readonly Tuple<int, string>[] _loginErrors =
        {
            Tuple.Create(3000007, "IncorrectPassword"),
            Tuple.Create(3000009, "CharacterDoesNotExist"),
            Tuple.Create(3000006, "CharacterAlreadyExists"),
            Tuple.Create(3000016, "ClientCouldNotAttachToServer"),
            Tuple.Create(3000017, "ClientCouldNotAttachToServer"),
            Tuple.Create(3000012, "AnotherCharacterOnline"),
            Tuple.Create(3000013, "ErrorInSynchronization"),
            Tuple.Create(3000005, "IdleTooLong"),
            Tuple.Create(-1, "CouldNotAttachServer"),
            Tuple.Create(-1, "CharacterTransferInProgress")
        };

        private static readonly Tuple<int, string>[] _errorCode =
        {
            Tuple.Create(3000018, "CharacterPasswordInvalid"),
            Tuple.Create(3000019, "ThatCharacterDoesNotExist"),
            Tuple.Create(3000020, "ThatCharacterIsBeingPlayed"),
            Tuple.Create(3000021, "CharacterIsNotOldEnough"),
            Tuple.Create(3000022, "CharacterIsQueuedForBackup"),
            Tuple.Create(3000023, "CouldntCarryOutYourRequest")
        };

        private static readonly Tuple<int, string>[] _pickUpErrors =
        {
            Tuple.Create(3000267, "YouCanNotPickThatUp"),
            Tuple.Create(3000268, "ThatIsTooFarAway"),
            Tuple.Create(3000269, "ThatIsOutOfSight"),
            Tuple.Create(3000270, "ThatItemDoesNotBelongToYou"),
            Tuple.Create(3000271, "YouAreAlreadyHoldingAnItem")
        };

        private static readonly Tuple<int, string>[] _generalErrors =
        {
            Tuple.Create(3000007, "IncorrectNamePassword"),
            Tuple.Create(3000034, "SomeoneIsAlreadyUsingThisAccount"),
            Tuple.Create(3000035, "YourAccountHasBeenBlocked"),
            Tuple.Create(3000036, "YourAccountCredentialsAreInvalid"),
            Tuple.Create(-1, "CommunicationProblem"),
            Tuple.Create(-1, "TheIGRConcurrencyLimitHasBeenMet"),
            Tuple.Create(-1, "TheIGRTimeLimitHasBeenMet"),
            Tuple.Create(-1, "GeneralIGRAuthenticationFailure"),
            Tuple.Create(3000037, "CouldntConnectToUO")
        };

        public static string GetError(byte packetID, byte code)
        {
            switch (packetID)
            {
                case 0x53:
                    if (code >= 10)
                    {
                        code = 9;
                    }

                    Tuple<int, string> t = _loginErrors[code];

                    return t.Item2;

                case 0x85:
                    if (code >= 6)
                    {
                        code = 5;
                    }

                    t = _errorCode[code];

                    return t.Item2;

                case 0x27:
                    if (code >= 5)
                    {
                        code = 4;
                    }

                    t = _pickUpErrors[code];

                    return t.Item2;

                case 0x82:
                    if(code == 255)
                    {
                        return "Could not create player accounts for load test. Make sure the server is setup properly to auto create multiple accounts per IP address!";
                    }
                    else if (code >= 9)
                    {
                        code = 8;
                    }

                    t = _generalErrors[code];

                    return t.Item2;
            }

            return string.Empty;
        }
    }
}