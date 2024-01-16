// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

using System;
using System.Collections;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using Celeste.Mod.Helpers;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

new CelesteAnalyzer.Sample.Examples().Bad();

namespace CelesteAnalyzer.Sample
{
    public class Examples
    {

        public void Bad()
        {
            using var hook = new ILHook(() => HookTarget(420), (ctx) =>
            {
                var cursor = new ILCursor(ctx);

                cursor.Emit(OpCodes.Ldarg_0);
                cursor.EmitDelegate(static (int f) => f * 2);
                cursor.EmitDelegate(Cb);
                cursor.EmitDelegate(Manipulator);
                cursor.Emit(OpCodes.Starg, 0);

                Console.WriteLine(ctx.ToString());
            });

            using var detour = new Hook(() => HookTarget(420), Detour);
            using var detour2 = new Hook(() => HookTarget(420), static (Action<int> orig, int arg) =>
            {
            });
            
            
            using var detour3 = new Hook(() => HookTarget(420), static (Action<int> orig, int arg) =>
            {
                orig(arg);
            });

            RandomEvent += (orig, arg) =>
            {

            };
            On.Celeste.Player.OnNormalUpdate += static (orig, arg, arg2) =>
            {
                return orig(arg, arg2);
            };
            On.Celeste.Player.OnSomeRoutine += RoutineOnHook;
            
            IL.Celeste.Player.NormalUpdate += static ctx =>
            {
                var cursor = new ILCursor(ctx);
                cursor.Remove();
                cursor.RemoveRange(4);
                cursor.TryGotoNext(MoveType.After, i => true, i => false);
                cursor.GotoNext(MoveType.After, i => true, i => false);
                cursor.GotoPrev(MoveType.After, i => true, i => false);
                cursor.TryGotoPrev(MoveType.After, i => true, i => false);
            };

            HookTarget(4);
        }

        private static IEnumerator RoutineOnHook(Func<IEnumerator> orig)
        {
            yield return new SwapImmediately(orig());

            var origRoutine = orig();
            while (origRoutine.MoveNext())
                yield return origRoutine.Current;
            
            
            yield return orig();
        }
        
        private int Cb(int arg)
        {
            return arg * 2;
        }

        private static int Manipulator(int arg) => arg * 2;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void HookTarget(int arg)
        {
            Console.WriteLine(arg);
        }

        private void Detour(Action<int> orig, int arg)
        {
            Console.WriteLine("Yo");
            orig(arg);
        }
        
        private void NotAHook(Action<int> orig, int arg)
        {
            Console.WriteLine("Yo");
        }
        
        public static event Action<Action<int>, int> RandomEvent;
    }

    [CustomEntity, Other]
    [Tracked]
    class MyEntity : Entity
    {
        public MyEntity(Vector2 blah)
        {
            var s = Scene;

            cb = () =>
            {
                Console.WriteLine(Scene);
                // we don't have access to this class and it's not tracked, no warning here
                new EntityList().FindAll<StringBuilder>();
                new EntityList().FindAll<MyEntity>();

                
                
                new Tracker().GetEntities<MyEntity>();
                new Tracker().GetEntities<OtherEntity>();
                new Tracker().GetEntities<MyTrigger>();
            };
        }

        public object DoStuff()
        {
            return (Scene, Other);
        }

        private Action cb;
    }

    [TrackedAs(typeof(MyEntity))]
    class OtherEntity : Entity
    {
        
    }

    [CustomEntity($"MyTrigger = {nameof(Generator)}")]
    //[TrackedAs(typeof(Trigger))]
    class MyTrigger : Trigger
    {
        void Generator(int a)
        {
            
        }
    }

    [Other]
    class Unrelated
    {
        
    }
}

namespace On.Celeste
{
    public class Player
    {
        public static event Func<Func<int, string, int>, int, string, int> OnNormalUpdate;
        
        public static event Func<Func<IEnumerator>, IEnumerator> OnSomeRoutine;
    }
}

namespace IL.Celeste
{
    public class Player
    {
        public static event Action<ILContext> NormalUpdate;
    }
}

namespace Celeste.Mod.Helpers
{
    public record SwapImmediately(IEnumerator Enumerable);
    
    public class CustomEntity : Attribute
    {
        public CustomEntity(params string[] ids)
        {
            
        }
    }
    
    public class Tracked : Attribute
    {
    }
    
    public class TrackedAs : Attribute
    {
        public TrackedAs(Type trackedAs)
        {
            
        }
    }

    public class Other : Attribute
    {
    }

    public class Entity
    {
        public object Scene { get; set; }
        
        public object Other { get; set; }
    }

    public class Trigger : Entity
    {
    }

    public class EntityList
    {
        public System.Collections.Generic.List<T> FindAll<T>()
        {
            return new();
        } 
    }

    public class Tracker
    {
        public System.Collections.Generic.List<Entity> GetEntities<T>() where T : Entity
        {
            return new();
        }
    }
}


