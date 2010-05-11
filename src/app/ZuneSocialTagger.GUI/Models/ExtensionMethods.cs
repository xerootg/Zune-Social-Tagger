using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ZuneSocialTagger.GUI.Models
{
    public static class ExtensionMethods
    {
        public static ObservableCollection<T> ToObservableCollection<T>(this IEnumerable<T> enumerable)
        {
            var collection = new ObservableCollection<T>();

            foreach (var item in enumerable)
                collection.Add(item);

            return collection;
        }

        public static void ForEach<T>(this IEnumerable<T> enumerable, Action<T> action)
        {
            foreach (var element in enumerable)
            {
                action(element);
            }
        }
    }
}