#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
namespace TechAssistXR.Hybrid.Editor
{
    public static class MachineWorkbenchBuilder
    {
        const string Root="Assets/TechAssistXRHybrid";
        [MenuItem("TechAssist XR/Create Machine AI Workbench")]
        public static void Create() {
            if(Application.isPlaying) return;
            TechnicianDemoBuilder.Create();
            if(!UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.StartsWith("TechnicianDesktopDemo")) return;
            var systems=GameObject.Find("TechnicianSystems");
            var session=systems.GetComponent<TechnicianSessionManager>();
            var diagnosis=systems.AddComponent<MachineDiagnosisTransport>();
            Set(diagnosis,"machineProfile",AssetDatabase.LoadAssetAtPath<TextAsset>(Root+"/machine_profile.json"));
            Set(session,"transport",diagnosis);
            Object.DestroyImmediate(systems.GetComponent<DemoTechnicianTransport>());
            var machine=GameObject.Find("MachineRoot").transform;
            while(machine.childCount>0) Object.DestroyImmediate(machine.GetChild(0).gameObject);
            BuildMachine(machine);
            var camera=GameObject.Find("Main Camera").GetComponent<Camera>();
            camera.transform.position=new Vector3(3.4f,2.7f,-6.5f); camera.transform.LookAt(new Vector3(0,0.9f,0));
            camera.fieldOfView=32;
            var canvas=GameObject.Find("TechnicianCanvas").transform;
            var original=GameObject.Find("ProcedurePanel");
            var top=Panel(canvas,"WorkbenchHeader",new Vector2(0.02f,0.90f),new Vector2(0.98f,0.99f));
            Text(top,"Heading","TECHASSIST XR   /   MACHINE ASSISTANT",new Vector2(0.02f,0.48f),new Vector2(0.61f,0.97f),24);
            var badge=Text(top,"ModeBadge","",new Vector2(0.02f,0.04f),new Vector2(0.61f,0.43f),15);
            badge.color=new Color(0.25f,0.85f,0.77f);
            var back=Button(top,"Machine info",new Vector2(0.81f,0.16f),new Vector2(0.98f,0.86f));
            var info=Panel(canvas,"MachineInfo",new Vector2(0.02f,0.09f),new Vector2(0.28f,0.88f));
            Text(info,"Label","MACHINE RECORD   /   A102",new Vector2(.06f,.91f),new Vector2(.95f,.98f),14);
            Text(info,"Name","Hydraulic\nPower Unit",new Vector2(.06f,.73f),new Vector2(.95f,.91f),30);
            Text(info,"Facts","Training asset • synthetic model\n\nOil reservoir / motor / pump\nSeal / return filter / controls\n\nStatus: inspection requested",new Vector2(.06f,.47f),new Vector2(.95f,.73f),18);
            Text(info,"ReportLabel","TECHNICIAN REPORT (typed transcript)",new Vector2(.06f,.39f),new Vector2(.95f,.46f),13);
            var report=Input(info);
            var analyze=Button(info,"Analyze report",new Vector2(.06f,.10f),new Vector2(.94f,.20f));
            Text(info,"Example","Example: Oil leaking near pump",new Vector2(.06f,.02f),new Vector2(.96f,.09f),14);
            var evidence=Panel(canvas,"EvidenceReview",new Vector2(.68f,.09f),new Vector2(.98f,.88f));
            Text(evidence,"EvidenceHeader","REPORT  →  SOURCES  →  PLAN",new Vector2(.05f,.92f),new Vector2(.95f,.98f),17);
            var status=Text(evidence,"AnalysisStatus","",new Vector2(.05f,.81f),new Vector2(.95f,.91f),15);
            status.color=new Color(.25f,.85f,.77f);
            var sourceText=Scroll(evidence,"MachineEvidence",new Vector2(.05f,.45f),new Vector2(.95f,.80f),15);
            var plan=Scroll(evidence,"ExtractedPlan",new Vector2(.05f,.15f),new Vector2(.95f,.43f),16);
            var repair=Button(evidence,"Review accepted • start guidance",new Vector2(.05f,.025f),new Vector2(.95f,.12f));
            Text(canvas,"ModelCaption","A102\n3D training model • select highlighted parts during guidance",new Vector2(.30f,.05f),new Vector2(.66f,.14f),17).alignment=TextAlignmentOptions.Center;
            var workbench=systems.AddComponent<MachineWorkbench>();
            Set(workbench,"diagnosis",diagnosis); Set(workbench,"session",session); Set(workbench,"reportInput",report);
            Set(workbench,"status",status); Set(workbench,"evidence",sourceText); Set(workbench,"plan",plan); Set(workbench,"badge",badge);
            Set(workbench,"analyzeButton",analyze); Set(workbench,"repairButton",repair); Set(workbench,"backButton",back);
            Set(workbench,"infoPage",info.gameObject); Set(workbench,"evidencePage",evidence.gameObject); Set(workbench,"procedurePanel",original);
            var backendButton=Button(top,"Backend",new Vector2(.64f,.16f),new Vector2(.79f,.86f));
            var connection=Panel(canvas,"BackendConnection",new Vector2(.27f,.20f),new Vector2(.73f,.82f));
            Text(connection,"ConnectionTitle","CONNECT TO TECHASSIST API",new Vector2(.05f,.87f),new Vector2(.95f,.97f),22);
            Text(connection,"URLLabel","Render service URL (https://…onrender.com)",new Vector2(.05f,.77f),new Vector2(.95f,.85f),16);
            var url=ConnectionInput(connection,"BackendUrl",new Vector2(.05f,.66f),new Vector2(.95f,.76f),false);
            Text(connection,"TokenLabel","Technician token — kept in memory only",new Vector2(.05f,.54f),new Vector2(.95f,.63f),16);
            var token=ConnectionInput(connection,"TechnicianToken",new Vector2(.05f,.43f),new Vector2(.95f,.54f),true);
            var connectionStatus=Text(connection,"ConnectionStatus","",new Vector2(.05f,.20f),new Vector2(.95f,.40f),17);
            var connect=Button(connection,"Connect",new Vector2(.05f,.04f),new Vector2(.33f,.16f));
            var offline=Button(connection,"Offline demo",new Vector2(.36f,.04f),new Vector2(.66f,.16f));
            var close=Button(connection,"Close",new Vector2(.69f,.04f),new Vector2(.95f,.16f));
            var settings=systems.AddComponent<BackendConnectionPanel>();
            Set(settings,"diagnosis",diagnosis); Set(settings,"workbench",workbench); Set(settings,"panel",connection.gameObject);
            Set(settings,"urlInput",url); Set(settings,"tokenInput",token); Set(settings,"status",connectionStatus);
            Set(settings,"openButton",backendButton); Set(settings,"connectButton",connect); Set(settings,"offlineButton",offline); Set(settings,"closeButton",close);
            connection.gameObject.SetActive(false);
            original.SetActive(false);
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),AssetDatabase.GenerateUniqueAssetPath(Root+"/Generated/TechnicianAIWorkbench.unity"));
            Selection.activeGameObject=systems;
        }
        static void BuildMachine(Transform root) {
            var shader=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");
            Material Mat(string n,Color c) {
                var m=new Material(shader){color=c};
                AssetDatabase.CreateAsset(m,AssetDatabase.GenerateUniqueAssetPath(Root+"/Generated/"+n+".mat"));return m;
            }
            var blue=Mat("PumpBlue",new Color(.08f,.36f,.49f));var steel=Mat("Steel",new Color(.4f,.48f,.52f));
            var dark=Mat("Rubber",new Color(.025f,.035f,.045f));var amber=Mat("SealAmber",new Color(1,.55f,.10f));
            var green=Mat("StatusGreen",new Color(.1f,.8f,.42f));var red=Mat("StopRed",new Color(.8f,.12f,.12f));
            var basePlate=Part(root,"Skid",PrimitiveType.Cube,new Vector3(0,.10f,0),new Vector3(2.6f,.16f,1.3f),steel);
            Part(root,"oil_reservoir",PrimitiveType.Cube,new Vector3(-.35f,.55f,.1f),new Vector3(1.65f,.75f,.90f),blue,true);
            Part(root,"motor_housing",PrimitiveType.Cylinder,new Vector3(-.45f,1.22f,.1f),new Vector3(.56f,.47f,.56f),steel,true).localRotation=Quaternion.Euler(0,0,90);
            for(int i=0;i<8;i++) Part(root,"CoolingFin"+i,PrimitiveType.Cylinder,new Vector3(-.85f+i*.11f,1.22f,.1f),new Vector3(.65f,.016f,.65f),dark).localRotation=Quaternion.Euler(0,0,90);
            Part(root,"pump_seal",PrimitiveType.Cylinder,new Vector3(.16f,1.22f,.1f),new Vector3(.34f,.07f,.34f),amber,true).localRotation=Quaternion.Euler(0,0,90);
            Part(root,"PumpBody",PrimitiveType.Cube,new Vector3(.45f,1.22f,.1f),new Vector3(.42f,.48f,.5f),blue);
            Part(root,"filter_cover",PrimitiveType.Cylinder,new Vector3(.91f,.83f,.1f),new Vector3(.34f,.30f,.34f),steel,true);
            Part(root,"FilterCap",PrimitiveType.Cylinder,new Vector3(.91f,1.15f,.1f),new Vector3(.40f,.04f,.4f),dark);
            var control=Part(root,"control_panel",PrimitiveType.Cube,new Vector3(-.65f,.76f,-.43f),new Vector3(.65f,.46f,.16f),dark,true);
            Part(root,"Display",PrimitiveType.Cube,new Vector3(-.7f,.83f,-.52f),new Vector3(.29f,.17f,.025f),green);
            Part(root,"EmergencyStop",PrimitiveType.Sphere,new Vector3(-.43f,.68f,-.55f),new Vector3(.105f,.105f,.08f),red);
            Part(root,"pressure_gauge",PrimitiveType.Cylinder,new Vector3(.43f,1.66f,.10f),new Vector3(.28f,.055f,.28f),steel,true).localRotation=Quaternion.Euler(90,0,0);
            Part(root,"isolation_valve",PrimitiveType.Sphere,new Vector3(1.12f,1.37f,.10f),new Vector3(.24f,.08f,.24f),red,true);
            Part(root,"GaugeFace",PrimitiveType.Cylinder,new Vector3(.43f,1.66f,.035f),new Vector3(.23f,.007f,.23f),dark).localRotation=Quaternion.Euler(90,0,0);
            Hose(root,"OilLine",dark,new[]{new Vector3(.65f,1.25f,.15f),new Vector3(.90f,1.43f,.15f),new Vector3(1.1f,1.2f,.15f),new Vector3(.95f,1.13f,.15f)});
            Hose(root,"ReturnLine",dark,new[]{new Vector3(.91f,.56f,.10f),new Vector3(.95f,.35f,-.25f),new Vector3(.4f,.35f,-.25f)});
            for(int i=0;i<4;i++) Part(root,"Foot"+i,PrimitiveType.Cube,new Vector3(i<2?-1:1,0,i%2==0?-.45f:.45f),new Vector3(.3f,.15f,.25f),dark);
            PrefabUtility.SaveAsPrefabAsset(root.gameObject,AssetDatabase.GenerateUniqueAssetPath(Root+"/Generated/HydraulicPowerUnit.prefab"));
        }
        static Transform Part(Transform parent,string name,PrimitiveType kind,Vector3 position,Vector3 scale,Material mat,bool selectable=false) {
            var go=GameObject.CreatePrimitive(kind);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=scale;
            go.GetComponent<Renderer>().sharedMaterial=mat;
            if(selectable) go.AddComponent<TechnicianComponent>();else Object.DestroyImmediate(go.GetComponent<Collider>());
            return go.transform;
        }
        static void Hose(Transform parent,string name,Material mat,Vector3[] points) {
            var go=new GameObject(name);go.transform.SetParent(parent,false);var line=go.AddComponent<LineRenderer>();line.useWorldSpace=false;line.positionCount=points.Length;line.SetPositions(points);line.widthMultiplier=.075f;line.numCornerVertices=8;line.numCapVertices=8;line.sharedMaterial=mat;
        }
        static RectTransform Panel(Transform parent,string name,Vector2 min,Vector2 max) {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);var rect=go.GetComponent<RectTransform>();Bounds(rect,min,max);
            go.GetComponent<Image>().color=new Color(.045f,.075f,.11f,.98f);return rect;
        }
        static void Bounds(RectTransform r,Vector2 min,Vector2 max) {r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;}
        static TMP_Text Text(Transform parent,string name,string value,Vector2 min,Vector2 max,int size) {
            var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);Bounds(go.GetComponent<RectTransform>(),min,max);
            var t=go.AddComponent<TextMeshProUGUI>();t.font=TMP_Settings.defaultFontAsset;t.text=value;t.fontSize=size;t.color=new Color(.85f,.92f,.96f);t.richText=false;t.raycastTarget=false;return t;
        }
        static Button Button(Transform parent,string label,Vector2 min,Vector2 max) {
            var rect=Panel(parent,label+"Button",min,max);rect.GetComponent<Image>().color=new Color(.06f,.40f,.43f);
            var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=rect.GetComponent<Image>();Text(rect,"Label",label,new Vector2(.03f,.03f),new Vector2(.97f,.97f),17).alignment=TextAlignmentOptions.Center;return button;
        }
        static TMP_InputField ConnectionInput(Transform parent,string name,Vector2 min,Vector2 max,bool password) {
            var rect=Panel(parent,name,min,max); rect.GetComponent<Image>().color=new Color(.12f,.17f,.22f);
            var text=Text(rect,"Text","",new Vector2(.02f,.05f),new Vector2(.98f,.95f),18);
            var field=rect.gameObject.AddComponent<TMP_InputField>();field.textViewport=rect;field.textComponent=(TextMeshProUGUI)text;
            field.characterLimit=2048;field.contentType=password?TMP_InputField.ContentType.Password:TMP_InputField.ContentType.Standard;
            return field;
        }
        static TMP_InputField Input(Transform parent) {
            var rect=Panel(parent,"TechnicianReport",new Vector2(.06f,.23f),new Vector2(.94f,.39f));rect.GetComponent<Image>().color=new Color(.12f,.17f,.22f);
            var text=Text(rect,"Text","",new Vector2(.04f,.04f),new Vector2(.96f,.96f),20);
            var field=rect.gameObject.AddComponent<TMP_InputField>();field.textViewport=rect;field.textComponent=(TextMeshProUGUI)text;field.lineType=TMP_InputField.LineType.MultiLineNewline;field.characterLimit=1000;field.text="Oil leaking near pump";return field;
        }
        static TMP_Text Scroll(Transform parent,string name,Vector2 min,Vector2 max,int size) {
            var rect=Panel(parent,name,min,max);rect.GetComponent<Image>().color=new Color(.06f,.10f,.14f);rect.gameObject.AddComponent<RectMask2D>();
            var text=Text(rect,"Content","",new Vector2(0,1),Vector2.one,size);text.rectTransform.pivot=new Vector2(.5f,1);
            text.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            var scroll=rect.gameObject.AddComponent<ScrollRect>();scroll.viewport=rect;scroll.content=text.rectTransform;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;return text;
        }
        static void Set(Object o,string name,Object value) {var so=new SerializedObject(o);so.FindProperty(name).objectReferenceValue=value;so.ApplyModifiedPropertiesWithoutUndo();}
    }
}
#endif
