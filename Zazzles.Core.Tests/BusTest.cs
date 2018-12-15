/*
    Copyright(c) 2014-2018 FOG Project

    The MIT License

    Permission is hereby granted, free of charge, to any person obtaining a copy
    of this software and associated documentation files(the "Software"), to deal
    in the Software without restriction, including without limitation the rights
    to use, copy, modify, merge, publish, distribute, sublicense, and / or sell
    copies of the Software, and to permit persons to whom the Software is
    furnished to do so, subject to the following conditions :
    The above copyright notice and this permission notice shall be included in
    all copies or substantial portions of the Software.
    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.IN NO EVENT SHALL THE
    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
    THE SOFTWARE.
*/

using System;
using System.IO;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Zazzles.Core.PubSub.IPC;

namespace Zazzles.Core.PubSub
{
    [TestClass]
    public class BusTest
    {
        [TestMethod]
        public void IPCAgent_Called_OnGlobalMessage()
        {
            // Arrange
            string srcMsg = "5";
            var mockLogger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<Bus>();
            var mockIPCLogger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<MockIPCAgent>();
            var mockParser = new MockParser();

            var mockAgent = new MockIPCAgent(mockIPCLogger, mockParser);
            var bus = new Bus(mockLogger, mockAgent);

            byte[] rawOut = new byte[0];
            var expected = mockParser.Serialize(srcMsg);

            mockAgent.Out += (sender, data) => {
                rawOut = data;
            };

            // Act
            bus.Publish(srcMsg, MessageScope.Global);

            // Assert
            Assert.AreNotEqual(0, rawOut.Length);
        }

        [TestMethod]
        public async Task Bus_Publishes_OnIPC()
        {
            // Arrange
            var mockLogger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<Bus>();
            var mockIPCLogger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<MockIPCAgent>();
            var mockParser = new MockParser();

            var mockAgent = new MockIPCAgent(mockIPCLogger, mockParser);
            var bus = new Bus(mockLogger, mockAgent);

            var payload = "foobar";
            var meta = new MetaData(MessageScope.Global, MessageOrigin.Self)
            {
                SentTimestamp = DateTime.Now
            };

            MetaData receivedMeta = null;
            string receivedPayload = null;

            bus.Subscribe<string>((msg) =>
            {
                receivedMeta = msg.MetaData;
                receivedPayload = msg.Payload;
            });

            // Act
            await mockAgent.FakeReceive(payload, meta);

            // Assert
            Assert.IsNotNull(receivedPayload);
            Assert.IsNotNull(receivedMeta);
            Assert.AreEqual(payload, receivedPayload);
            Assert.AreEqual(meta.SentTimestamp, receivedMeta.SentTimestamp);
            Assert.AreEqual(MessageOrigin.Remote, receivedMeta.Origin);
            Assert.AreEqual(MessageScope.Global, receivedMeta.Scope);
        }


        [TestMethod]
        public void IPCAgent_NotCalled_OnLocalMessage()
        {
            // Arrange
            var mockLogger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<Bus>();
            var mockIPCLogger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<MockIPCAgent>();
            var mockParser = new MockParser();

            var mockAgent = new MockIPCAgent(mockIPCLogger, mockParser);
            var bus = new Bus(mockLogger, mockAgent);

            var payload = "foobar";

            bool ipcCalled = false;

            mockAgent.Out += (sender, data) => {
                ipcCalled = true;
            };

            // Act
            bus.Publish(payload, MessageScope.Local);

            // Assert
            Assert.IsFalse(ipcCalled);
        }

        [TestMethod]
        public void Subscribed_ActionGets_CalledOnPublish()
        {
            // Arrange
            var srcMsg = new Wrapper1(5);
            Wrapper1 subMsg = null;
            var mockLogger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<Bus>();
            var bus = new Bus(mockLogger);
            bus.Subscribe<Wrapper1> ((msg) => 
            {
                subMsg = msg.Payload;
            });

            // Act
            bus.Publish(srcMsg, MessageScope.Local);

            // Assert
            Assert.AreEqual(srcMsg, subMsg);
        }

        [TestMethod]
        public void Subscribed_ActionDoesNotGet_CalledOnPublishOfDifferentType()
        {
            // Arrange
            bool triggered = false;
            var mockLogger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<Bus>();
            var bus = new Bus(mockLogger);
            bus.Subscribe<Wrapper1>((msg) =>
            {
                triggered = true;
            });

            // Act
            bus.Publish("5", MessageScope.Local);

            // Assert
            Assert.IsFalse(triggered);
        }

        [TestMethod]
        public void Unsubscribed_ActionDoesNotGet_CalledOnPublish()
        {
            // Arrange
            var srcMsg = new Wrapper1(5);
            var wasCalled = false;
            var mockLogger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<Bus>();
            var bus = new Bus(mockLogger);

            var testAction = new Action<Message<Wrapper1>>((msg) =>
            {
                wasCalled = true;
            });

            bus.Subscribe(testAction);
            bus.Unsubscribe(testAction);

            // Act
            bus.Publish(srcMsg, MessageScope.Local);

            // Assert
            Assert.IsFalse(wasCalled);
        }

        [TestMethod]
        public void Bus_IPC_RoundTrip()
        {
            // Arrange
            var mockBusLogger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<Bus>();
            var mockIPCLogger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<MockIPCAgent>();

            var mockParser1 = new MockParser();
            var mockParser2 = new MockParser();
            var mockAgent1 = new MockIPCAgent(mockIPCLogger, mockParser1);
            var mockAgent2 = new MockIPCAgent(mockIPCLogger, mockParser2);
            var bus1 = new Bus(mockBusLogger, mockAgent1);
            var bus2 = new Bus(mockBusLogger, mockAgent2);

            mockAgent1.Out += (sender, data) =>
            {
                mockAgent2.Relay(data);
            };

            var payload = "foobar";

            MetaData receivedMeta = null;
            string receivedPayload = null;

            bus2.Subscribe<string>((msg) =>
            {
                receivedMeta = msg.MetaData;
                receivedPayload = msg.Payload;
            });

            // Act
            bus1.Publish(payload, MessageScope.Global);

            // Assert
            Assert.IsNotNull(receivedPayload);
            Assert.IsNotNull(receivedMeta);
            Assert.AreEqual(payload, receivedPayload);
            Assert.AreEqual(MessageOrigin.Remote, receivedMeta.Origin);
            Assert.AreEqual(MessageScope.Global, receivedMeta.Scope);
        }
    }

    abstract class AbstractWrapper
    {
        public int i;
        public AbstractWrapper(int i)
        {
            this.i = i;
        }
    }

    class Wrapper1 : AbstractWrapper
    {
        public Wrapper1(int i) : base(i)
        {
        }
    }

    class Wrapper2 : AbstractWrapper
    {
        public Wrapper2(int i) : base(i)
        {
        }
    }

    class MockIPCAgent : AbstractIPCAgent
    {
        public event EventHandler<byte[]> Out;
        public event EventHandler<byte[]> In;

        public MockIPCAgent(ILogger<MockIPCAgent> logger, IParser parser) : base(logger, parser)
        {
            In += onIn;
        }

        private void onIn(object sender, byte[] msg)
        {
            OnReceive(msg).Wait();
        }

        public override async Task<bool> Connect() => true;

        public override async Task<bool> Disconnect() => true;


        public override void Dispose()
        {

        }

        protected override async Task<bool> Send(byte[] msg)
        {
            Out.Invoke(this, msg);
            return true;
        }

        public void Relay(byte[] data)
        {
            In.Invoke(this, data);
        }

        public async Task FakeReceive<T>(T payload, MetaData meta) where T : class
        {
            // construct a fake transport
            byte[] serialized = await _parser.Serialize(payload);
            var transport = new Transport(typeof(T), serialized, meta);
            byte[] serializedTransport = await _parser.Serialize(transport);
            Relay(serializedTransport);
        }
    }

    class MockParser : IParser
    {
        public async Task<T> Deserialize<T>(byte[] obj) where T : class
        {
            var serializer = new DataContractSerializer(typeof(T));
            using (var stream = new MemoryStream(obj))
            {
                return (T)serializer.ReadObject(stream);
            }
        }

        public async Task<byte[]> Serialize<T>(T obj) where T : class
        {
            using (var stream = new MemoryStream())
            {
                var serializer = new DataContractSerializer(typeof(T));
                serializer.WriteObject(stream, obj);

                return stream.ToArray();
            }
        }
    }
}
