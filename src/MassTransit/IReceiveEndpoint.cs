// Copyright 2007-2016 Chris Patterson, Dru Sellers, Travis Smith, et. al.
//  
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use
// this file except in compliance with the License. You may obtain a copy of the 
// License at 
// 
//     http://www.apache.org/licenses/LICENSE-2.0 
// 
// Unless required by applicable law or agreed to in writing, software distributed
// under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR 
// CONDITIONS OF ANY KIND, either express or implied. See the License for the 
// specific language governing permissions and limitations under the License.
namespace MassTransit
{
    using GreenPipes;
    using Pipeline;
    using Transports;


    /// <summary>
    /// A service endpoint has a inbound transport that pushes messages to consumers
    /// </summary>
    public interface IReceiveEndpoint :
        IConsumePipeConnector,
        IRequestPipeConnector,
        IReceiveObserverConnector,
        IReceiveEndpointObserverConnector,
        IConsumeObserverConnector,
        IConsumeMessageObserverConnector,
        IProbeSite
    {
        /// <summary>
        /// Starts recieving from the inbound transport.
        /// </summary>
        /// <returns>A handle to the receiving endpoint, which is used to stop it</returns>
        ReceiveEndpointHandle Start();
    }
}