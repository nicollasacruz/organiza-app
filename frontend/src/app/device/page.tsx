import type {Metadata} from 'next';
import {DeviceScreen} from '@/components/device';

export const metadata:Metadata = {title:'Ligar a TV · Organiza', robots:{index:false, follow:false}, referrer:'no-referrer'};

export default function Page(){return <DeviceScreen/>;}
