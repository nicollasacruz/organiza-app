"use client";
import {useEffect} from 'react';
import {useRouter} from 'next/navigation';
export default function Home(){const router=useRouter();useEffect(()=>router.replace('/ganhos/'),[router]);return <p className="loading-inline">A abrir a Organiza…</p>;}
