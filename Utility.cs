using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace FieldVisualizer
{
    public class Utility 
    {
        /// <summary>
        /// Returns all Types that derive from given type
        /// </summary>
        /// <param name="targetType"></param>
        /// <returns></returns>
        public static Type[] GetDerivedTypes(Type targetType, bool noAbstract = true, bool returnBase = true)
        {
            var result = (from domainAssembly in AppDomain.CurrentDomain.GetAssemblies()
                                  from assemblyType in domainAssembly.GetTypes()
                                  where ((assemblyType.IsSubclassOf(targetType) || (assemblyType == targetType && returnBase)) 
                                  && (!assemblyType.IsAbstract || !noAbstract ))
                                  select assemblyType).ToArray<Type>();
            return result;
        }

        /// <summary>
        /// Returns all Types that implement the interface.
        /// </summary>
        /// <returns></returns>
        public static Type[] GetTypesImplementInterface(Type _interface,bool noAbstract = true) 
        {
            if (!_interface.IsInterface)
                return null;

            var result = (from domainAssembly in AppDomain.CurrentDomain.GetAssemblies()
                               from assemblyType in domainAssembly.GetTypes()
                               where _interface.IsAssignableFrom(assemblyType) && !assemblyType.IsAbstract
                               select assemblyType).ToArray<Type>();
            return result;
        }

        /// <summary>
        /// Equal with System.Activator.CreateInstance(Type type) but can Create strings.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static object CreateInstanceOf(Type type, int rank = 0)
        {
            if (type == typeof(string))
            {
                string a = "";
                return a;
            }
            else if (!typeof(IList).IsAssignableFrom(type))
                return Activator.CreateInstance(type);
            else
                return Activator.CreateInstance(type, rank);
        }


    }
}

namespace Extensions.Array
{
    using System;

    public static class ArrayExtensions
    {
        public static void Resize(this Array Array, ref Array mArray, int newSize)
        {
            if (newSize == mArray.Length)
                return;

            Array temp = (Array)Activator.CreateInstance(mArray.GetType(), newSize);
            if (mArray.Length < newSize)
                mArray.CopyTo(temp, 0);
            else
                Array.Copy(mArray, temp, newSize);

            mArray = temp;
        }
    }
}