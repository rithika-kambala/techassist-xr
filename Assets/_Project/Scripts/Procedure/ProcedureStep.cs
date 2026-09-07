using System;

[Serializable]
public class ProcedureStep
{
    public int step_number;
    public string title;
    public string instruction;
    public string target_component;
}

[Serializable]
public class ProcedureData
{
    public string machine_id;
    public string procedure_id;
    public ProcedureStep[] steps;
}
