using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System;
using UnityEditor;
using UnityEngine;
using System.Runtime.Serialization;
using Extensions.Array;

namespace FieldVisualizer
{
    public class GenericFieldVisualizer
    {

        private Dictionary<long, int> countKeeper = new Dictionary<long, int>();
        private Dictionary<long, bool> expandKeeper = new Dictionary<long, bool>();
        private ObjectIDGenerator idGenerator = new ObjectIDGenerator();
        private Type tempObjType = null;
        private object helperObj1 = null;
        private int helperIndex = -1;
        private long collectionIdHelper = 0;
        private GUILayoutOption[] guiLayoutOptions = new GUILayoutOption[] { GUILayout.MaxWidth(250) };
        private bool isFirst;

        public bool debugOn { get; set; }
        public bool nonPublic { get; set; }
        public bool errorsOn { get; set; }

        /// <summary>
        /// Use only if options do not cover the occasion
        /// </summary>
        public BindingFlags bindingFlags { get; set; }

        #region Ctor
        public GenericFieldVisualizer(bool debugOn = false, bool nonPublic = false)
        {
            this.debugOn = debugOn;
            this.nonPublic = nonPublic;
            if (nonPublic)
                bindingFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public;
            else
                bindingFlags = BindingFlags.Public | BindingFlags.Instance;
        }

        public GenericFieldVisualizer(BindingFlags bindingFlags, bool debugOn = false)
        {
            this.debugOn = debugOn;
            this.bindingFlags = bindingFlags;
        }

        #endregion

        /// <summary>
        /// Use Inside OnGUI method
        /// </summary>
        /// <param name="target"></param>
        /// <param name="nonPublic"></param>
        public void VisualizeFields(object target)
        {
            Type targetType = target.GetType();
            FieldInfo[] myFields = targetType.GetFields(bindingFlags);

            for (int i = 0; i < myFields.Length; i++)
            {
                try
                {
                    VisualizeField(myFields[i], target);
                }
                catch (Exception e)
                {
                    if(!errorsOn)
                        Debug.Log(e);
                    Debug.LogError(e);
                }
            }
        }

        private void VisualizeSingleColumnCollection(FieldInfo myField, object target, GUILayoutOption[] guiLayoutOptionsOverride = null)
        {
            if (myField.GetValue(target) == null)
                myField.SetValue(target, Utility.CreateInstanceOf(myField.FieldType));

            IList collection = (IList)myField.GetValue(target);
            bool isFirst;
            long currentId = idGenerator.GetId(collection, out isFirst);
            GUILayoutOption[] guiLayoutOptions = guiLayoutOptionsOverride ?? this.guiLayoutOptions;
            //for Array GetElementType for list GenericArguments... 
            Type elementType = myField.FieldType.GetElementType() ?? myField.FieldType.GetGenericArguments()[0];

            if (isFirst)
            {
                expandKeeper.Add(currentId, false);
                countKeeper.Add(currentId, 0);
                if (myField.GetValue(target) != null)
                {
                    IList temp = (IList)myField.GetValue(target);
                    countKeeper[currentId] = temp.Count;
                }
            }
            expandKeeper[currentId] = EditorGUILayout.Foldout(expandKeeper[currentId], myField.Name, true);
            Rect controlRect = GUILayoutUtility.GetLastRect();
            #region rightClick
            Event e = Event.current;
            if (e.type == EventType.MouseUp && e.button == 1 && controlRect.Contains(e.mousePosition))
            {
                GenericMenu menu = new GenericMenu();
                Type[] allTypes = Utility.GetDerivedTypes(elementType);
                foreach (Type type in allTypes)
                {
                    menu.AddItem(new GUIContent("Add:/" + type.ToString()), false, () => { tempObjType = type; countKeeper[currentId]++; });
                }
                menu.AddSeparator("");
                menu.AddItem(new GUIContent("Clear Collection"), false, () => countKeeper[currentId] = 0);
                menu.ShowAsContext();
            }
            #endregion
            if (!expandKeeper[currentId])
                return;

            EditorGUI.indentLevel++;
            int count = 0;
            GUI.SetNextControlName(currentId.ToString());

            if (collection == null)         
                count = EditorGUILayout.IntField("Size:", 0, guiLayoutOptions);
            else
                count = EditorGUILayout.IntField("Size:", collection.Count, guiLayoutOptions);
            if (count < 0)
                count = 0;

            if (GUI.changed && GUI.GetNameOfFocusedControl() == currentId.ToString())
                countKeeper[currentId] = count;
            #region Add/Remove elements 
            if (collection != null)
            {
                if (e.keyCode == KeyCode.Escape)
                    countKeeper[currentId] = collection.Count;
                else if ((collection.Count != countKeeper[currentId]) && (GUI.GetNameOfFocusedControl() != currentId.ToString() || e.keyCode == KeyCode.Return))
                {
                    if (myField.FieldType.IsArray)
                    {
                            Array temp = (Array)collection;
                            temp.Resize(ref temp, countKeeper[currentId]);
                            myField.SetValue(target, temp);
                            collection = temp;
                            if (tempObjType != null)
                            {
                                collection[collection.Count - 1] = Utility.CreateInstanceOf(tempObjType);
                                tempObjType = null;
                            }

                            long newId = idGenerator.GetId(collection, out isFirst);
                            if (isFirst)
                            {
                                countKeeper.Add(newId, collection.Count);
                                expandKeeper.Add(newId, true);
                                if (expandKeeper.ContainsKey(currentId))
                                    expandKeeper.Remove(currentId);
                                if (countKeeper.ContainsKey(currentId))
                                    countKeeper.Remove(currentId);
                            }
                    }
                    else
                    {
                        if (countKeeper[currentId] > collection.Count)
                        {
                            for (int i = collection.Count; i < countKeeper[currentId]; i++)
                            {
                                if (elementType.IsPrimitive)
                                    collection.Add(Utility.CreateInstanceOf(elementType));
                                else
                                {
                                    collection.Add(tempObjType == null ? null : Utility.CreateInstanceOf(tempObjType));
                                    tempObjType = null;
                                }
                            }
                        }
                        else if (countKeeper[currentId] < collection.Count)
                            for (int i = collection.Count - 1; i >= countKeeper[currentId]; i--)
                                collection.RemoveAt(i);
                    }
                }
            }
            #endregion
            #region Collection Visualization
            //100 lines later ...
            if (collection != null && collection.Count != 0)
                for (int i = 0; i < collection.Count; i++)
                {
                    collection[i] = VisualizeCollectionElement(collection[i], currentId, i, elementType);
                }
            EditorGUI.indentLevel--;
            #endregion
        }

        private object VisualizeSingleColumnCollection(IList collection, int index = -1, Type type = null, GUILayoutOption[] guiLayoutOptionsOverride = null)
        {
            
                if (collection == null && type == null)
                    return null;
                if (collection != null)
                    type = collection.GetType();

                if (collection == null)
                    collection = (IList)Utility.CreateInstanceOf(type);                

                bool isFirst;
                long currentId = idGenerator.GetId(collection, out isFirst);
                GUILayoutOption[] guiLayoutOptions = guiLayoutOptionsOverride ?? this.guiLayoutOptions;
                //for Array GetElementType for list GenericArguments... 
                Type elementType = type.GetElementType() ?? type.GetGenericArguments()[0];
                if (isFirst)
                {
                    expandKeeper.Add(currentId, false);
                    countKeeper.Add(currentId, 0);
                        IList temp = collection;
                        countKeeper[currentId] = temp.Count;
                }
                expandKeeper[currentId] = EditorGUILayout.Foldout(expandKeeper[currentId], "Element " + index, true);
                Rect controlRect = GUILayoutUtility.GetLastRect();

                #region rightClick
                Event e = Event.current;
                if (e.type == EventType.MouseUp && e.button == 1 && controlRect.Contains(e.mousePosition))
                {
                    GenericMenu menu = new GenericMenu();
                    Type[] allTypes = Utility.GetDerivedTypes(elementType);
                    foreach (Type t in allTypes)
                        menu.AddItem(new GUIContent("Add:/" + t.ToString()), false, () => { tempObjType = t; countKeeper[currentId]++; });

                    menu.AddSeparator("");
                    menu.AddItem(new GUIContent("Clear Collection"), false, () => countKeeper[currentId] = 0);
                    menu.ShowAsContext();
                }
                #endregion
                if (!expandKeeper[currentId])
                    return collection;

                EditorGUI.indentLevel++;
                int count = 0;
                GUI.SetNextControlName(currentId.ToString());

                if (collection == null)
                    count = EditorGUILayout.IntField("Size:", 0, guiLayoutOptions);
                else
                    count = EditorGUILayout.IntField("Size:", collection.Count, guiLayoutOptions);
                if (count < 0)
                    count = 0;

                if (GUI.changed && GUI.GetNameOfFocusedControl() == currentId.ToString())
                    countKeeper[currentId] = count;
                #region Add/Remove elements 
                if (collection != null || true)
                {
                    if (e.keyCode == KeyCode.Escape)
                        countKeeper[currentId] = collection.Count;
                    else if ((collection.Count != countKeeper[currentId]) && (GUI.GetNameOfFocusedControl() != currentId.ToString() || e.keyCode == KeyCode.Return))
                    {
                        if (type.IsArray)
                        {
                            Array temp = (Array)collection;
                            temp.Resize(ref temp, countKeeper[currentId]);
                            collection = temp;
                            if (tempObjType != null)
                            {
                                collection[collection.Count - 1] = Utility.CreateInstanceOf(tempObjType);
                                tempObjType = null;
                            }

                            long newId = idGenerator.GetId(collection, out isFirst);
                            if (isFirst)
                            {
                                countKeeper.Add(newId, collection.Count);
                                expandKeeper.Add(newId, true);
                                if (expandKeeper.ContainsKey(currentId))
                                    expandKeeper.Remove(currentId);
                                if (countKeeper.ContainsKey(currentId))
                                    countKeeper.Remove(currentId);
                            }
                        }
                        else
                        {
                            if (countKeeper[currentId] > collection.Count)
                            {
                                for (int i = collection.Count; i < countKeeper[currentId]; i++)
                                {
                                    if (elementType.IsPrimitive)
                                        collection.Add(Utility.CreateInstanceOf(elementType));
                                    else
                                    {
                                        collection.Add(tempObjType == null ? null : Utility.CreateInstanceOf(tempObjType));
                                        tempObjType = null;
                                    }
                                }
                            }
                            else if (countKeeper[currentId] < collection.Count)
                                for (int i = collection.Count - 1; i >= countKeeper[currentId]; i--)
                                    collection.RemoveAt(i);
                        }
                    }
                }
                #endregion
                #region Collection Visualization
                //100 lines later ...
                if (collection != null && collection.Count != 0)
                    for (int i = 0; i < collection.Count; i++)
                    {

                        collection[i] = VisualizeCollectionElement(collection[i], currentId, i, elementType);

                    }
                EditorGUI.indentLevel--;
                #endregion
            
            return collection;
        }

        /// <summary>
        /// Creates GUI fields for Collection Elements 
        /// </summary>
        /// <param name="type"></param>
        /// <param name="target"></param>
        private object VisualizeCollectionElement(object target, long collectionId, int index = -1, Type type = null, GUILayoutOption[] guiLayoutOptionsOverride = null)
        {
            if (target == null && type == null)
                return null;

            GUILayoutOption[] guiLayoutOptions = guiLayoutOptionsOverride ?? this.guiLayoutOptions;

            string label;

            if (index >= 0)
                label = "Element " + index.ToString() + " :";
            else
                label = "Element: ";

            if (type == null)
                type = target.GetType();

            if (type == typeof(int))
            {
                target = EditorGUILayout.IntField(label, (int)target, guiLayoutOptions);
            }
            else if (type == typeof(string))      //STRING
            {
                target = EditorGUILayout.TextField(label, (string)target, guiLayoutOptions);
            }
            else if (type == typeof(float))       //FLOAT
            {
                target = EditorGUILayout.FloatField(label, (float)target);
            }
            else if (type == typeof(bool))      //BOOL
            {
                target = EditorGUILayout.Toggle(label, (bool)target, guiLayoutOptions);
            }
            else if (type.IsEnum)          //ENUM
            {
                target = EditorGUILayout.EnumPopup(label, (Enum)target, guiLayoutOptions);
            }
            else if (typeof(IList).IsAssignableFrom(type))    //collection
            {
                
                target = VisualizeSingleColumnCollection((IList)target,  index, type);
            }
            else if (type.IsSubclassOf(typeof(UnityEngine.Object)))
            {
                target = EditorGUILayout.ObjectField(label, (UnityEngine.Object)target, type, true, guiLayoutOptions);
            }
            #region Class/Interface
            else if (type.IsClass || type.IsInterface)
            {
                Rect rect;
                if (target == null)
                {
                    EditorGUILayout.LabelField("Null Element " + index.ToString(), guiLayoutOptions);
                    rect = GUILayoutUtility.GetLastRect();
                }
                else
                {
                    long currentID = idGenerator.GetId(target, out isFirst);
                    if (isFirst)
                        expandKeeper.Add(currentID, false);

                    expandKeeper[currentID] = EditorGUILayout.Foldout(expandKeeper[currentID], target.ToString() +" "+ index +" :", true);
                    rect = GUILayoutUtility.GetLastRect();
                    EditorGUI.indentLevel++;

                    if (expandKeeper[currentID])
                        if (target != null)
                            VisualizeFields(target);

                    EditorGUI.indentLevel--;
                }

                Event e = Event.current;
                if (e.button == 1 && e.type == EventType.MouseUp && rect.Contains(e.mousePosition))
                {
                    Type[] allTypes = type.IsClass ? Utility.GetDerivedTypes(type) : Utility.GetTypesImplementInterface(type);
                    GenericMenu menu = new GenericMenu();

                    string title;
                    if (target == null)
                        title = "Create new/";
                    else
                        title = "Change to/";
                    if (allTypes != null)
                        foreach (Type t in allTypes)
                            menu.AddItem(new GUIContent(title + t.Name), false, () => { helperObj1 = Utility.CreateInstanceOf(t); collectionIdHelper = collectionId; helperIndex = index; });
                    else
                        Debug.Log("No Valid Types Present!");

                    if (target != null)
                    {
                        menu.AddSeparator("");
                        menu.AddItem(new GUIContent("Clear"), false, () => { helperIndex = index; collectionIdHelper = collectionId; });
                    }
                    menu.ShowAsContext();

                }
                if (helperIndex == index && collectionId == collectionIdHelper)
                {
                    target = helperObj1;
                    helperObj1 = null;
                    helperIndex = -1;
                    collectionIdHelper = 0;
                }
                #endregion
            }
            return target;
        }


        /// <summary>
        /// Creates Input GUI Field
        /// </summary>
        /// <param name="myField"></param>
        private void VisualizeField(FieldInfo myField, object target, bool debugOn = false, GUILayoutOption[] guiLayoutOptionsOverride = null)
        {
            if (target == null)
                return;
            GUILayoutOption[] guiLayoutOptions = guiLayoutOptionsOverride ?? this.guiLayoutOptions;

            if (myField.FieldType == typeof(int))        //INT
            {
                myField.SetValue(target, EditorGUILayout.IntField(myField.Name, (int)myField.GetValue(target), guiLayoutOptions));
            }
            else if (myField.FieldType == typeof(string))      //STRING
            {
                myField.SetValue(target, EditorGUILayout.TextField(myField.Name, (string)myField.GetValue(target), guiLayoutOptions));
            }
            else if (myField.FieldType == typeof(float))       //FLOAT
            {
                myField.SetValue(target, EditorGUILayout.FloatField(myField.Name, (float)myField.GetValue(target), guiLayoutOptions));
            }
            else if (myField.FieldType == typeof(bool))      //BOOL
            {
                myField.SetValue(target, EditorGUILayout.Toggle(myField.Name, (bool)myField.GetValue(target), guiLayoutOptions));
            }
            else if (myField.FieldType.IsEnum)          //ENUM
            {
                myField.SetValue(target, EditorGUILayout.EnumPopup(myField.Name, (Enum)myField.GetValue(target), guiLayoutOptions));
            }
            else if (myField.FieldType.IsSubclassOf(typeof(UnityEngine.Object)))      //Anything that derives from UnityEngine.Object 
            {
                myField.SetValue(target, EditorGUILayout.ObjectField(myField.Name, (UnityEngine.Object)myField.GetValue(target), myField.FieldType, true, guiLayoutOptions));
            }
            else if (typeof(IList).IsAssignableFrom(myField.FieldType))
            {
                VisualizeSingleColumnCollection(myField, target);
            }else if (typeof(IDictionary).IsAssignableFrom(myField.FieldType))
            {
                if(debugOn)
                    Debug.Log("Dictionary Not Supported");
            }
            #region Class/Interface
            else if (myField.FieldType.IsClass || myField.FieldType.IsInterface)
            {
                Rect rect;
                if (myField.GetValue(target) == null)
                {
                    EditorGUILayout.LabelField(myField.Name + " ~Null", guiLayoutOptions);
                    rect = GUILayoutUtility.GetLastRect();
                }
                else
                {
                    object currentObj = myField.GetValue(target);
                    bool isFirst;
                    long currentId = idGenerator.GetId(currentObj, out isFirst);
                    if (isFirst)
                        expandKeeper.Add(currentId, false);

                    expandKeeper[currentId] = EditorGUILayout.Foldout(expandKeeper[currentId], myField.Name, true);
                    rect = GUILayoutUtility.GetLastRect();
                    if (expandKeeper[currentId])
                    {
                        EditorGUI.indentLevel++;
                        VisualizeFields(currentObj);
                        EditorGUI.indentLevel--;
                    }

                }
                Event e = Event.current;

                if (e.button == 1 && e.type == EventType.MouseUp && rect.Contains(e.mousePosition))
                {
                    Type[] allTypes = myField.FieldType.IsClass ? Utility.GetDerivedTypes(myField.FieldType) : Utility.GetTypesImplementInterface(myField.FieldType);
                    GenericMenu menu = new GenericMenu();
                    string title;
                    if (myField.GetValue(target) == null)
                        title = "Create/";
                    else
                        title = "Change to/";

                    if (allTypes != null)
                    {
                        foreach (Type type in allTypes)
                        {
                            menu.AddItem(new GUIContent(title + type.ToString()), false, () => myField.SetValue(target, Utility.CreateInstanceOf(type)));
                        }
                    }
                    if (myField.GetValue(target) != null)
                    {
                        menu.AddSeparator("");
                        menu.AddItem(new GUIContent("Clear Field"), false, () => myField.SetValue(target, null));
                    }
                    menu.ShowAsContext();
                }
            }
            #endregion
            else
            {
                Debug.Log("Unsupported Type Detected-> " + myField.FieldType);
            }
        }

    }
}



