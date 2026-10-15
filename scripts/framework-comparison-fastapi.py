"""A small local comparison; independent of HTD PAY and its database."""
import sys
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'docs/report-revision/comparison-runtime'))
from fastapi import FastAPI
from pydantic import BaseModel, Field
import uvicorn

app=FastAPI()
class ComparisonInput(BaseModel):
    amount:int=Field(ge=1,le=1_000_000)

@app.middleware('http')
async def trace(request,call_next):
    response=await call_next(request)
    response.headers['X-Lab-Trace']='fastapi'
    return response

@app.post('/comparison/validate')
async def validate(input:ComparisonInput):
    return {'amount':input.amount}

if __name__=='__main__':
    uvicorn.run(app,host='127.0.0.1',port=5096,access_log=False)
