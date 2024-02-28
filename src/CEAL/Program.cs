using Ai.Hgb.Dat.Communication;
using Ai.Hgb.Dat.Configuration;
using System;

namespace CEAL.Main {
  public class Program {
    public static void Main(string[] args) {
      Console.WriteLine("Graph Algorithm Engine V0");

      //client
      var cts = new CancellationTokenSource();
      HostAddress address = new HostAddress("127.0.0.1", 1883);
      var converter = new JsonPayloadConverter();

      //MqttBroker broker = new MqttBroker(address, true, true, 5000, "mqtt");
      //broker.StartUp();

      ISocket socket1 = new MqttSocket("socket1", "socket1", address, converter, connect: true);
      ISocket socket2 = new MqttSocket("socket2", "socket2", address, converter, connect: true);
      string group = Guid.NewGuid().ToString();

      // local osga
      var alg1 = new OSGAm("ga1", group, socket1);
      var alg2 = new OSGAm("ga2", group, socket2);
      alg2.PopSize = 2000;      
      alg2.MutationRate = 0.5;


      var alg1Task = Task.Run(() => alg1.Run(42, 1000));
      Task.Delay(200);
      var alg2Task = Task.Run(() => alg2.Run(24, 1000));

      Task.WaitAll(new[] { alg1Task, alg2Task });

      // tear down
      Task.Delay(1000).Wait();
      socket1.Disconnect();
      socket2.Disconnect();
      //broker.TearDown();


      var problem = new Rastrigin(problemSize: 100);
      //var problem = new Ackley(problemSize: 2);

      // ga + es
      //var algGA = new PGA<double[]>("pga1", problem, populationSize: 1000, generations: 1000, mutationRate: 0.1);
      //var algES = new PES("pes1", problem, generations: 5);
      //algGA.LocalSearchRate = 0.01;
      //algGA.LocalSearch = algES.Execute();
      //var algGATask = algGA.Run();
      //var algGAResult = algGATask.Result;
      //Console.WriteLine($"\n\n\nBest solution candidate found: {algGAResult.Item2}");
      //Console.WriteLine($"\n{string.Join(", ", algGAResult.Item1)}");

      // es
      //var algES = new PES("pes1", problem);
      //var algESTask = algES.Run();
      //var algESResult = algESTask.Result;
      //Console.WriteLine($"\n\n\nBest solution candidate found: {algESResult.Item2}");
      //Console.WriteLine($"\n{string.Join(", ", algESResult.Item1)}");

      // V2
      //var algGA2 = new PGA<double[]>("pga2", problem, populationSize: 1000, generations: 1000, mutationRate: 0.1);
      //var service = new GAServices<double[]>("127.0.0.1");
      //algGA2.EpochTriggeringFailureRate = 0.1;
      //algGA2.ImmigrationRate = 0.1;
      //algGA2.Migrate = service.Migrate;
      //algGA2.Immigrate = service.Immigrate;

      //var algGA2Task = algGA2.Run();
      //var algGA2Result = algGA2Task.Result;
      //Console.WriteLine($"\n\n\nBest solution candidate found: {algGA2Result.Item2}");
      //Console.WriteLine($"\n{string.Join(", ", algGA2Result.Item1)}");

    }
  }
}